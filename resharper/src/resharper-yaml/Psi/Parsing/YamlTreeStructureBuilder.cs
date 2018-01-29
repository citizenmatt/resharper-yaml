using System;
using JetBrains.Annotations;
using JetBrains.DataFlow;
using JetBrains.ReSharper.Plugins.Yaml.Psi.Tree.Impl;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.ReSharper.Psi.TreeBuilder;
using JetBrains.Text;

namespace JetBrains.ReSharper.Plugins.Yaml.Psi.Parsing
{
  // General indent error handling tactic:
  // If a node's first token doesn't have correct indentation, don't read the rest of it.
  // If any other part of a node has incorrect indentation, add an error element and reset
  // the expected indentation to be the rest of the element
  // If we don't follow this, we either rollback (and fail to parse the construct at all)
  // or break out of parsing that node and potentially be out of sync for the rest of the file
  internal class YamlTreeStructureBuilder : TreeStructureBuilderBase, IPsiBuilderTokenFactory
  {
    private int myDocumentStartLexeme;
    private int myCurrentLineIndent;

    public YamlTreeStructureBuilder(ILexer<int> lexer, Lifetime lifetime)
      : base(lifetime)
    {
      Builder = new PsiBuilder(lexer, ElementType.YAML_FILE, this, lifetime);
    }

    protected override string GetExpectedMessage(string name)
    {
      return ParserMessages.GetExpectedMessage(name);
    }

    protected override PsiBuilder Builder { get; }

    protected override TokenNodeType NewLine => YamlTokenType.NEW_LINE;
    protected override NodeTypeSet CommentsOrWhiteSpacesTokens => YamlTokenType.COMMENTS_OR_WHITE_SPACES;

    public LeafElementBase CreateToken(TokenNodeType tokenNodeType, IBuffer buffer, int startOffset, int endOffset)
    {
      if (tokenNodeType == YamlTokenType.NS_ANCHOR_NAME
          || tokenNodeType == YamlTokenType.NS_CHARS
          || tokenNodeType == YamlTokenType.NS_PLAIN_ONE_LINE
          || tokenNodeType == YamlTokenType.NS_TAG_CHARS
          || tokenNodeType == YamlTokenType.NS_URI_CHARS
          || tokenNodeType == YamlTokenType.NS_WORD_CHARS
          || tokenNodeType == YamlTokenType.SCALAR_TEXT
          || tokenNodeType == YamlTokenType.C_DOUBLE_QUOTED_SINGLE_LINE
          || tokenNodeType == YamlTokenType.C_SINGLE_QUOTED_SINGLE_LINE)
      {
        return tokenNodeType.Create(IdentifierIntern.Intern(buffer, startOffset, endOffset));
      }

      return tokenNodeType.Create(buffer, new TreeOffset(startOffset), new TreeOffset(endOffset));
    }

    public void ParseFile()
    {
      var mark = MarkNoSkipWhitespace();

      do
      {
        ParseDocument();
      } while (!Builder.Eof());

      Done(mark, ElementType.YAML_FILE);
    }

    private void ParseDocument()
    {
      // TODO: Can we get indents in this prefix?
      // TODO: Should the document prefix be part of the document node?
      SkipLeadingWhitespace();

      if (Builder.Eof())
        return;

      var mark = MarkNoSkipWhitespace();

      ParseDirectives();

      if (GetTokenType() != YamlTokenType.DOCUMENT_END)
      {
        myDocumentStartLexeme = Builder.GetCurrentLexeme();
        ParseBlockNode(-1, true);
      }

      while (!Builder.Eof() && GetTokenType() != YamlTokenType.DOCUMENT_END)
        Advance();

      if (!Builder.Eof())
        ExpectToken(YamlTokenType.DOCUMENT_END);

      Done(mark, ElementType.YAML_DOCUMENT);
    }

    private void ParseDirectives()
    {
      var tt = GetTokenType();
      if (tt != YamlTokenType.PERCENT && tt != YamlTokenType.DIRECTIVES_END)
        return;

      var mark = MarkNoSkipWhitespace();

      do
      {
        var curr = Builder.GetCurrentLexeme();

        ParseDirective();

        if (curr == Builder.GetCurrentLexeme())
          break;
      } while (!Builder.Eof() && GetTokenType() != YamlTokenType.DIRECTIVES_END);

      if (!Builder.Eof())
        ExpectToken(YamlTokenType.DIRECTIVES_END);

      DoneBeforeWhitespaces(mark, ElementType.DIRECTIVES);
    }

    private void ParseDirective()
    {
      if (GetTokenType() != YamlTokenType.PERCENT)
        return;

      var mark = MarkNoSkipWhitespace();

      ExpectToken(YamlTokenType.PERCENT);

      bool parsed;
      do
      {
        parsed = ExpectTokenNoSkipWhitespace(YamlTokenType.NS_CHARS);
        ParseSeparateInLine();
      } while (parsed && !Builder.Eof() && GetTokenTypeNoSkipWhitespace() != YamlTokenType.NEW_LINE && GetTokenTypeNoSkipWhitespace() != YamlTokenType.COMMENT);

      if (GetTokenTypeNoSkipWhitespace() == YamlTokenType.NEW_LINE)
        Advance();

      // We don't care about the indent here. We're at the start of the doc. The first block
      // node will handle its own indent
      ParseTrailingCommentLines();

      Done(mark, ElementType.DIRECTIVE);
    }

    // block-in being "inside a block sequence"
    // NOTE! This method is not guaranteed to consume any tokens! Protect against endless loops!
    private void ParseBlockNode(int expectedIndent, bool isBlockIn)
    {
      if (!TryParseBlockInBlock(expectedIndent, isBlockIn))
        ParseFlowInBlock(expectedIndent);
    }

    private bool TryParseBlockInBlock(int expectedIndent, bool isBlockIn)
    {
      return TryParseBlockScalar(expectedIndent) || TryParseBlockCollection(expectedIndent, isBlockIn);
    }

    private bool TryParseBlockScalar(int expectedIndent)
    {
      var mark = MarkNoSkipWhitespace();

      var correctIndent = ParseSeparationSpace(expectedIndent + 1);
      if (correctIndent)
      {
        // Start the node after the whitespace. It's just nicer.
        var scalarMark = MarkNoSkipWhitespace();

        if (TryParseNodeProperties(expectedIndent + 1))
          correctIndent = ParseSeparationSpace(expectedIndent + 1);

        if (!correctIndent)
        {
          ErrorBeforeWhitespaces("Invalid indent");
          expectedIndent = myCurrentLineIndent;
        }

        var tt = GetTokenTypeNoSkipWhitespace();
        if (tt == YamlTokenType.PIPE)
        {
          ParseBlockScalar(expectedIndent, scalarMark, tt, ElementType.LITERAL_SCALAR_NODE);
          Builder.Drop(mark);
          return true;
        }

        if (tt == YamlTokenType.GT)
        {
          ParseBlockScalar(expectedIndent, scalarMark, tt, ElementType.FOLDED_SCALAR_NODE);
          Builder.Drop(mark);
          return true;
        }
      }

      Builder.RollbackTo(mark);
      return false;
    }

    private void ParseBlockScalar(int expectedIndent, int mark, TokenNodeType indicator, CompositeNodeType elementType)
    {
      ExpectToken(indicator);

      // Scalar indent is either calculated from an indentation indicator,
      // or set to -1 to indicate auto-detect
      var scalarIndent = ParseBlockHeader(expectedIndent);
      ParseMultilineScalarText(scalarIndent);

      DoneBeforeWhitespaces(mark, elementType);
    }

    private void ParseMultilineScalarText(int expectedIndent)
    {
      // Keep track of the end of the value. We'll roll back to here at the
      // end. We only update it when we have valid content, or a valid indent
      // If we get something else (new lines or invalid content) we'll advance
      // but not move this forward, giving us somewhere to roll back to
      var endOfValueMark = MarkNoSkipWhitespace();

      // Skip leading whitespace, but not NEW_LINE or INDENT. Unlikely to get this, tbh
      SkipSingleLineWhitespace();

      var tt = GetTokenTypeNoSkipWhitespace();
      while (tt == YamlTokenType.SCALAR_TEXT || tt == YamlTokenType.INDENT || tt == YamlTokenType.NEW_LINE)
      {
        // Note that the lexer has handled some indent details for us, too.
        // The lexer will create INDENT tokens of any leading whitespace that
        // is equal or greater to the start of the block scalar. If it matches
        // content before the indent, it doesn't get treated as SCALAR_TEXT
        if (expectedIndent == -1 && tt == YamlTokenType.INDENT)
          expectedIndent = GetTokenLength();

        if (tt == YamlTokenType.SCALAR_TEXT || (tt == YamlTokenType.INDENT && GetTokenLength() > expectedIndent) || tt == YamlTokenType.NEW_LINE) 
        {
          Advance();

          // Keep track of the last place that we had either valid content or indent
          // We'll roll back to here in the case of e.g. a too-short indent
          Builder.Drop(endOfValueMark);
          endOfValueMark = MarkNoSkipWhitespace();
        }
        else
          Advance();

        SkipSingleLineWhitespace();
        tt = GetTokenTypeNoSkipWhitespace();
      }

      Builder.RollbackTo(endOfValueMark);
    }

    private int ParseBlockHeader(int expectedIndent)
    {
      var tt = GetTokenType();
      if (tt != YamlTokenType.NS_DEC_DIGIT && tt != YamlTokenType.PLUS && tt != YamlTokenType.MINUS)
        return -1;

      var mark = MarkNoSkipWhitespace();

      int relativeIndent;
      if (tt == YamlTokenType.NS_DEC_DIGIT)
      {
        relativeIndent = ParseDecDigit(expectedIndent);
        ParseChompingIndicator();
      }
      else
      {
        // We already know it's PLUS or MINUS
        ParseChompingIndicator();
        relativeIndent = ParseDecDigit(expectedIndent);
      }

      DoneBeforeWhitespaces(mark, ElementType.BLOCK_HEADER);

      return relativeIndent;
    }

    private int ParseDecDigit(int expectedIndent)
    {
      if (GetTokenType() == YamlTokenType.NS_DEC_DIGIT)
      {
        int.TryParse(Builder.GetTokenText(), out var relativeIndent);
        Advance();
        return expectedIndent + relativeIndent;
      }

      return -1;
    }

    private void ParseChompingIndicator()
    {
      var tt = GetTokenType();
      if (tt == YamlTokenType.PLUS || tt == YamlTokenType.MINUS)
        Advance();
    }

    private bool TryParseBlockCollection(int expectedIndent, bool isBlockIn)
    {
      var mark = MarkNoSkipWhitespace();

      var parsedProperties = false;
      var propertiesMark = MarkNoSkipWhitespace();
      var correctIndent = ParseSeparationSpace(expectedIndent + 1);
      if (correctIndent && TryParseNodeProperties(expectedIndent + 1))
      {
        parsedProperties = true;
        Builder.Drop(propertiesMark);
      }
      else
        Builder.RollbackTo(propertiesMark);

      correctIndent = ParseCommentLines(expectedIndent);
      if (!correctIndent)
      {
        // We're in between constructs, try to recover
        if (parsedProperties)
        {
          ErrorBeforeWhitespaces("Invalid indent");
          expectedIndent = myCurrentLineIndent;
        }
        else
        {
          Builder.RollbackTo(mark);
          return false;
        }
      }

      var tt = GetTokenType();
      if (tt == YamlTokenType.MINUS)
      {
        // Nested block sequences may be indented one less space, because people
        // intuitively see `-` as indent
        ParseBlockSequence(isBlockIn ? expectedIndent : expectedIndent - 1, mark);
      }
      else if (tt == YamlTokenType.QUESTION)
        ParseBlockMapping(expectedIndent, mark);
      else
        return TryParseImplicitBlockMapping(mark);

      return true;
    }

    private void ParseBlockSequence(int expectedIndent, int mark)
    {
      // We pass in indent of (n), we need (n+m), where m is auto-detected
      // The current line indent is (n+m)
      // We are already at the end of end of an indent point, having
      // just parsed s-l-comments
      // This knowledge/coupling is something I dislike about the spec
      if (myCurrentLineIndent > 0)
        expectedIndent = Math.Max(expectedIndent, myCurrentLineIndent);

      do
      {
        var curr = Builder.GetCurrentLexeme();

        if (!TryParseBlockSequenceEntry(expectedIndent))
          break;

        if (curr == Builder.GetCurrentLexeme())
          break;
      } while (!Builder.Eof() && LookAheadSkipComments(1) != null);

      DoneBeforeWhitespaces(mark, ElementType.BLOCK_SEQUENCE_NODE);
    }

    [MustUseReturnValue]
    private bool TryParseBlockSequenceEntry(int expectedIndent)
    {
      var mark = MarkNoSkipWhitespace();

      // TODO: Remove this extra check for MINUS
      // I've added it as a workaround for a test that fails because compact mapping isn't implemented yet
      if (ParseIndent(expectedIndent) && GetTokenTypeNoSkipWhitespace() == YamlTokenType.MINUS)
      {
        ExpectToken(YamlTokenType.MINUS);
        ParseBlockNode(expectedIndent, true);

        DoneBeforeWhitespaces(mark, ElementType.SEQUENCE_ENTRY);
        return true;
      }

      Builder.RollbackTo(mark);
      return false;
    }

    private void ParseBlockMapping(int expectedIndent, int mark)
    {
      // We pass in indent of (n), we need (n+m), where m is auto-detected
      // The current line indent is (n+m)
      // We are already at the end of end of an indent point, having
      // just parsed s-l-comments
      // This knowledge/coupling is something I dislike about the spec
      if (myCurrentLineIndent > 0)
        expectedIndent = Math.Max(expectedIndent, myCurrentLineIndent);

      do
      {
        var curr = Builder.GetCurrentLexeme();

        if (!TryParseBlockMapEntry(expectedIndent))
          break;

        if (curr == Builder.GetCurrentLexeme())
          break;
      } while (!Builder.Eof());

      DoneBeforeWhitespaces(mark, ElementType.BLOCK_MAPPING_NODE);
    }

    [MustUseReturnValue]
    private bool TryParseBlockMapEntry(int expectedIndent)
    {
      return TryParseBlockMapExplicitEntry(expectedIndent) || TryParseBlockMapImplicitEntry(expectedIndent);
    }

    [MustUseReturnValue]
    private bool TryParseBlockMapExplicitEntry(int expectedIndent)
    {
      var mark = MarkNoSkipWhitespace();

      if (ParseIndent(expectedIndent))
      {
        ExpectTokenNoSkipWhitespace(YamlTokenType.QUESTION);
        ParseBlockNode(expectedIndent, false);

        var valueMark = MarkNoSkipWhitespace();

        if (ParseIndent(expectedIndent) && GetTokenTypeNoSkipWhitespace() == YamlTokenType.COLON)
        {
          Advance();
          ParseBlockNode(expectedIndent, false);
          Builder.Drop(valueMark);
        }
        else
          DoneBeforeWhitespaces(valueMark, ElementType.EMPTY_SCALAR_NODE);
        DoneBeforeWhitespaces(mark, ElementType.BLOCK_MAPPING_ENTRY);
        return true;
      }
      Builder.RollbackTo(mark);
      return false;
    }

    [MustUseReturnValue]
    private bool TryParseBlockMapImplicitEntry(int expectedIndent)
    {
      return false;
    }

    private bool TryParseImplicitBlockMapping(int mark)
    {
      // TODO: Handle implicit keys
      Builder.RollbackTo(mark);
      return false;
    }

    private void ParseFlowInBlock(int expectedIndent)
    {
      var mark = MarkNoSkipWhitespace();

      var correctIndent = ParseSeparationSpace(expectedIndent + 1);
      if (!correctIndent)
      {
        // If we rollback and return here, it means ParseBlock completely
        // failed, and we didn't parse anything - no tokens at all. We will
        // continue parsing, so we need to make sure we don't get stuck in
        // an endless loop. We're safe at the root of the document as we won't
        // get an incorrect indent.
        Builder.RollbackTo(mark);
        return;
      }

      ParseFlowNode(expectedIndent + 1);
      ParseTrailingCommentLines();

      Builder.Drop(mark);
    }

    private void ParseFlowNode(int expectedIndent)
    {
      var tt = GetTokenType();
      if (tt == YamlTokenType.ASTERISK)
        ParseAliasNode();
      else
        ParseFlowContent(expectedIndent);
    }

    private void ParseAliasNode()
    {
      var mark = MarkNoSkipWhitespace();
      ExpectToken(YamlTokenType.ASTERISK);
      ExpectTokenNoSkipWhitespace(YamlTokenType.NS_ANCHOR_NAME);
      DoneBeforeWhitespaces(mark, ElementType.ALIAS_NODE);
    }

    private bool TryParseNodeProperties(int expectedIndent)
    {
      var tt = GetTokenType();
      if (tt != YamlTokenType.BANG && tt != YamlTokenType.BANG_LT && tt != YamlTokenType.AMP)
        return false;

      var mark = MarkNoSkipWhitespace();

      if (tt == YamlTokenType.BANG || tt == YamlTokenType.BANG_LT)
      {
        ParseTagProperty();
        ParseAnchorProperty(expectedIndent);
      }
      else if (tt == YamlTokenType.AMP)
      {
        ParseAnchorProperty();
        ParseTagProperty(expectedIndent);
      }

      DoneBeforeWhitespaces(mark, ElementType.NODE_PROPERTIES);
      return true;
    }

    private void ParseAnchorProperty(int expectedIndent = -1)
    {
      var mark = MarkNoSkipWhitespace();

      // Only parse indent if we're in between properties
      var correctIndent = true;
      if (expectedIndent != -1)
        correctIndent = ParseSeparationSpace(expectedIndent);

      var tt = GetTokenType();
      if (tt == YamlTokenType.AMP && correctIndent)
      {
        var anchorMark = MarkNoSkipWhitespace();
        ExpectToken(YamlTokenType.AMP);
        ExpectTokenNoSkipWhitespace(YamlTokenType.NS_ANCHOR_NAME);
        DoneBeforeWhitespaces(anchorMark, ElementType.ANCHOR_PROPERTY);
        Builder.Drop(mark);
        return;
      }

      Builder.RollbackTo(mark);
    }

    private void ParseTagProperty(int expectedIndent = -1)
    {
      var mark = MarkNoSkipWhitespace();

      // Only parse indent if we're in between properties
      var correctIndent = true;
      if (expectedIndent != -1)
        correctIndent = ParseSeparationSpace(expectedIndent);

      var tt = GetTokenType();
      if (tt == YamlTokenType.BANG_LT && correctIndent)
      {
        ParseVerbatimTagProperty(mark);
        return;
      }

      if (tt == YamlTokenType.BANG && correctIndent)
      {
        if (LookAheadNoSkipWhitespaces(1).IsWhitespace)
          ParseNonSpecificTagProperty(mark);
        else
          ParseShorthandTagProperty(mark);
        return;
      }

      Builder.RollbackTo(mark);
    }

    private void ParseVerbatimTagProperty(int mark)
    {
      ExpectToken(YamlTokenType.BANG_LT);
      ExpectTokenNoSkipWhitespace(YamlTokenType.NS_URI_CHARS);
      ExpectTokenNoSkipWhitespace(YamlTokenType.GT);
      DoneBeforeWhitespaces(mark, ElementType.VERBATIM_TAG_PROPERTY);
    }

    private void ParseShorthandTagProperty(int mark)
    {
      ParseTagHandle();
      var tt = GetTokenTypeNoSkipWhitespace();
      // TODO: Is TAG_CHARS a superset of ns-plain?
      // TODO: Perhaps we should accept all text and add an inspection for invalid chars?
      if (tt != YamlTokenType.NS_TAG_CHARS && tt != YamlTokenType.NS_PLAIN_ONE_LINE)
        ErrorBeforeWhitespaces(ParserMessages.GetExpectedMessage("text"));
      else
        Advance();
      DoneBeforeWhitespaces(mark, ElementType.SHORTHAND_TAG_PROPERTY);
    }

    private void ParseTagHandle()
    {
      var mark = MarkNoSkipWhitespace();
      ExpectToken(YamlTokenType.BANG);
      var elementType = ParseSecondaryOrNamedTagHandle();
      DoneBeforeWhitespaces(mark, elementType);
    }

    private CompositeNodeType ParseSecondaryOrNamedTagHandle()
    {
      // Make sure we don't try to match a primary tag handle followed by ns-plain. E.g. `!foo`
      var tt = GetTokenTypeNoSkipWhitespace();
      var la = LookAheadNoSkipWhitespaces(1);
      if (tt.IsWhitespace || ((tt == YamlTokenType.NS_WORD_CHARS || tt == YamlTokenType.NS_TAG_CHARS) &&
                              la != YamlTokenType.BANG))
      {
        return ElementType.PRIMARY_TAG_HANDLE;
      }

      if (tt != YamlTokenType.NS_WORD_CHARS && tt != YamlTokenType.NS_TAG_CHARS && tt != YamlTokenType.BANG)
      {
        ErrorBeforeWhitespaces(ParserMessages.GetExpectedMessage("text", YamlTokenType.BANG.TokenRepresentation));
        return ElementType.NAMED_TAG_HANDLE;
      }

      var elementType = ElementType.SECONDARY_TAG_HANDLE;
      if (tt != YamlTokenType.BANG)
      {
        Advance();  // CHARS
        elementType = ElementType.NAMED_TAG_HANDLE;
      }
      ExpectTokenNoSkipWhitespace(YamlTokenType.BANG);

      return elementType;
    }

    private void ParseNonSpecificTagProperty(int mark)
    {
      ExpectToken(YamlTokenType.BANG);
      DoneBeforeWhitespaces(mark, ElementType.NON_SPECIFIC_TAG_PROPERTY);
    }

    private void ParseFlowContent(int expectedIndent)
    {
      var mark = MarkNoSkipWhitespace();

      // We're already expected to be at the correct indent, ParseFlowNode has checked
      var correctIndent = true;
      if (TryParseNodeProperties(expectedIndent))
        correctIndent = ParseSeparationSpace(expectedIndent);

      if (!correctIndent && IsNonEmptyFlowContent())
      {
        ErrorBeforeWhitespaces("Invalid indent");
        expectedIndent = myCurrentLineIndent;
      }

      CompositeNodeType elementType;
      var tt = GetTokenTypeNoSkipWhitespace();
      if (tt == YamlTokenType.LBRACK)
        elementType = ParseFlowSequence(expectedIndent);
      else if (tt == YamlTokenType.LBRACE)
        elementType = ParseFlowMapping(expectedIndent);
      else if (IsDoubleQuoted(tt))
      {
        Advance();
        elementType = ElementType.DOUBLE_QUOTED_SCALAR_NODE;
      }
      else if (IsSingleQuoted(tt))
      {
        Advance();
        elementType = ElementType.SINGLE_QUOTED_SCALAR_NODE;
      }
      else if (IsPlainScalarToken(tt))
        elementType = ParseMultilinePlainScalar(expectedIndent);
      else
        elementType = ElementType.EMPTY_SCALAR_NODE;

      DoneBeforeWhitespaces(mark, elementType);
    }

    private bool IsNonEmptyFlowContent()
    {
      var tt = GetTokenTypeNoSkipWhitespace();
      return tt == YamlTokenType.LBRACK || tt == YamlTokenType.RBRACK
                                        || IsDoubleQuoted(tt) || IsSingleQuoted(tt) || IsPlainScalarToken(tt);
    }

    private CompositeNodeType ParseFlowSequence(int expectedIndent)
    {
      ExpectToken(YamlTokenType.LBRACK);

      if (!ParseOptionalSeparationSpace(expectedIndent))
      {
        ErrorBeforeWhitespaces("Invalid indent");
        expectedIndent = myCurrentLineIndent;
      }

      ParseFlowSequenceEntry(expectedIndent);

      // Don't update expectedIndent - we have closing indicators, so we should know
      // where things are
      if (!ParseOptionalSeparationSpace(expectedIndent))
        ErrorBeforeWhitespaces("Invalid indent");

      if (GetTokenType() == YamlTokenType.COMMA)
      {
        do
        {
          ExpectToken(YamlTokenType.COMMA);

          if (!ParseOptionalSeparationSpace(expectedIndent))
            ErrorBeforeWhitespaces("Invalid indent");

          if (GetTokenType() != YamlTokenType.RBRACK)
            ParseFlowSequenceEntry(expectedIndent);
        } while (!Builder.Eof() && GetTokenTypeNoSkipWhitespace() != YamlTokenType.RBRACK && GetTokenTypeNoSkipWhitespace() == YamlTokenType.COMMA);
      }

      // TODO: Remove this
      // Only required to fix issues with tests while compact notation not yet implemented
      while (GetTokenType() != YamlTokenType.RBRACK)
        Advance();

      ExpectToken(YamlTokenType.RBRACK);

      return ElementType.FLOW_SEQUENCE_NODE;
    }

    private void ParseFlowSequenceEntry(int expectedIndent)
    {
      var mark = MarkNoSkipWhitespace();

      if (!TryParseFlowPair(expectedIndent))
        ParseFlowNode(expectedIndent);

      DoneBeforeWhitespaces(mark, ElementType.FLOW_SEQUENCE_ENTRY);
    }

    private bool TryParseFlowPair(int expectedIndent)
    {
      // TODO: Compact flow pair notation
      return false;
    }

    private CompositeNodeType ParseFlowMapping(int expectedIndent)
    {
      ExpectToken(YamlTokenType.LBRACE);

      if (!ParseOptionalSeparationSpace(expectedIndent))
      {
        ErrorBeforeWhitespaces("Invalid indent");
        expectedIndent = myCurrentLineIndent;
      }

      ParseFlowMapEntry(expectedIndent);

      // Don't update expectedIndent - we have closing indicators, so we should know
      // where things are
      if (!ParseOptionalSeparationSpace(expectedIndent))
        ErrorBeforeWhitespaces("Invalid indent");

      if (GetTokenType() == YamlTokenType.COMMA)
      {
        do
        {
          ExpectToken(YamlTokenType.COMMA);

          if (!ParseOptionalSeparationSpace(expectedIndent))
            ErrorBeforeWhitespaces("Invalid indent");

          if (GetTokenType() != YamlTokenType.RBRACE)
            ParseFlowMapEntry(expectedIndent);
        } while (!Builder.Eof() && GetTokenTypeNoSkipWhitespace() != YamlTokenType.RBRACE && GetTokenTypeNoSkipWhitespace() == YamlTokenType.COMMA);
      }

      ExpectToken(YamlTokenType.RBRACE);

      return ElementType.FLOW_MAPPING_NODE;
    }

    private void ParseFlowMapEntry(int expectedIndent)
    {
      // TODO: Parse flow map entry
      while (!Builder.Eof() && GetTokenType() != YamlTokenType.COMMA && GetTokenType() != YamlTokenType.RBRACE)
        Advance();
    }

    private CompositeNodeType ParseMultilinePlainScalar(int expectedIndent)
    {
      var endOfValueMark = -1;

      var tt = GetTokenTypeNoSkipWhitespace();
      while (IsPlainScalarToken(tt) || tt == YamlTokenType.INDENT || tt == YamlTokenType.NEW_LINE)
      {
        Advance();

        if (IsPlainScalarToken(tt))
        {
          if (endOfValueMark != -1 && myCurrentLineIndent < expectedIndent)
            break;

          if (endOfValueMark != -1)
            Builder.Drop(endOfValueMark);
          endOfValueMark = MarkNoSkipWhitespace();
        }

        SkipSingleLineWhitespace();
        tt = GetTokenTypeNoSkipWhitespace();
      }

      if (endOfValueMark != -1)
        Builder.RollbackTo(endOfValueMark);

      return ElementType.PLAIN_SCALAR_NODE;
    }

    private static bool IsPlainScalarToken(TokenNodeType tt)
    {
      return tt == YamlTokenType.NS_PLAIN_ONE_LINE || tt == YamlTokenType.NS_PLAIN_MULTI_LINE;
    }

    private new void Advance()
    {
      if (Builder.Eof())
        return;

      var tt = Builder.GetTokenType();
      if (tt == YamlTokenType.NEW_LINE)
        myCurrentLineIndent = 0;
      else if (tt == YamlTokenType.INDENT)
        myCurrentLineIndent = GetTokenLength();
      base.Advance();
    }

    protected override void SkipWhitespaces()
    {
      base.SkipWhitespaces();
      if (JustSkippedNewLine)
        myCurrentLineIndent = 0;
    }

    private int GetTokenLength()
    {
      var token = Builder.GetToken();
      return token.End - token.Start;
    }

    private TokenNodeType GetTokenTypeNoSkipWhitespace()
    {
      // this.GetTokenType() calls SkipWhitespace() first
      return Builder.GetTokenType();
    }

    private bool ExpectTokenNoSkipWhitespace(NodeType token, bool dontSkipSpacesAfter = false)
    {
      if (GetTokenTypeNoSkipWhitespace() != token)
      {
        var message = (token as TokenNodeType)?.GetDescription() ?? token.ToString();
        ErrorBeforeWhitespaces(GetExpectedMessage(message));
        return false;
      }
      if (dontSkipSpacesAfter)
        Builder.AdvanceLexer();
      else
        Advance();
      return true;
    }

    // ReSharper disable once UnusedMember.Local
    [Obsolete("Skips whitespace. Be explicit and call MarkSkipWhitespace", true)]
    private new int Mark()
    {
      throw new InvalidOperationException("Do not call Mark - be explicit about whitespace");
    }

    // ReSharper disable once UnusedMember.Local
    [MustUseReturnValue]
    private int MarkSkipWhitespace()
    {
      return base.Mark();
    }

    [MustUseReturnValue]
    private int MarkNoSkipWhitespace()
    {
      // this.Mark() calls SkipWhitespace() first
      return Builder.Mark();
    }

    // s-l-comments
    // Skipping whitespace, NL, comment and INDENT has the same effect,
    // except this will also skip trailing INDENT tokens
    [MustUseReturnValue]
    private bool ParseCommentLines(int expectedIndent)
    {
      _EatWhitespaceAndIndent();
      return myCurrentLineIndent >= expectedIndent;
    }

    // We don't care about the final indent. The next construct will have
    // to assert it itself
    private void ParseTrailingCommentLines()
    {
      _EatWhitespaceAndIndent();
    }

    // s-indent(n)
    [MustUseReturnValue]
    private bool ParseIndent(int expectedIndent)
    {
      // TODO: This should be INDENT char only!
      _EatWhitespaceAndIndent();
      return myCurrentLineIndent >= expectedIndent;
    }

    private void ParseSeparateInLine()
    {
      while (!Builder.Eof())
      {
        var tt = GetTokenTypeNoSkipWhitespace();
        if (tt == YamlTokenType.NEW_LINE || !tt.IsWhitespace)
          return;

        Advance();
      }
    }

    // s-separate(n)
    // Note that this isn't valid for flow-key or block-key
    [MustUseReturnValue]
    private bool ParseSeparationSpace(int expectedIndent)
    {
      // Either skip whitespace on the same line, or skip
      // empty lines, whitespace, comments and indents. If
      // ending on different line, must match expectedIndent
      var seenNewLine = false;
      var curr = Builder.GetCurrentLexeme();
      while (!Builder.Eof() && IsWhitespaceNewLineIndentOrComment())
      {
        if (GetTokenTypeNoSkipWhitespace() == YamlTokenType.NEW_LINE)
          seenNewLine = true;
        Advance();
      }

      // No whitespace at all!
      var thisLexeme = Builder.GetCurrentLexeme();
      if (thisLexeme == curr && thisLexeme != myDocumentStartLexeme)
        return false;

      // Seen a new line, therefore must be indented correctly
      if (seenNewLine)
        return myCurrentLineIndent >= expectedIndent;

      return true;
    }

    [MustUseReturnValueAttribute]
    private bool ParseOptionalSeparationSpace(int expectedIndent)
    {
      return !IsWhitespaceNewLineIndentOrComment() || ParseSeparationSpace(expectedIndent);
    }

    private void SkipLeadingWhitespace()
    {
      _EatWhitespaceAndIndent();
    }

    // If you're calling this, you're probably calling the wrong method
    private void _EatWhitespaceAndIndent()
    {
      while (!Builder.Eof() && IsWhitespaceNewLineIndentOrComment())
        Advance();
    }

    private bool IsWhitespaceNewLineIndentOrComment()
    {
      // GetTokenType skips WS, NL and comments, but let's be explicit
      var tt = GetTokenTypeNoSkipWhitespace();
      return tt == YamlTokenType.INDENT || tt == YamlTokenType.NEW_LINE || tt.IsComment || tt.IsWhitespace;
    }

    private void SkipSingleLineWhitespace()
    {
      while (!Builder.Eof())
      {
        var tt = GetTokenTypeNoSkipWhitespace();
        if (tt == YamlTokenType.NEW_LINE || (!tt.IsWhitespace && !tt.IsComment))
          return;

        Advance();
      }
    }

    private bool IsDoubleQuoted(TokenNodeType tt)
    {
      return tt == YamlTokenType.C_DOUBLE_QUOTED_MULTI_LINE || tt == YamlTokenType.C_DOUBLE_QUOTED_SINGLE_LINE;
    }

    private bool IsSingleQuoted(TokenNodeType tt)
    {
      return tt == YamlTokenType.C_SINGLE_QUOTED_MULTI_LINE || tt == YamlTokenType.C_SINGLE_QUOTED_SINGLE_LINE;
    }
  }
}