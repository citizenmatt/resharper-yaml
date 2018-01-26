using JetBrains.DataFlow;
using JetBrains.ReSharper.Plugins.Yaml.Psi.Tree.Impl;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.ReSharper.Psi.TreeBuilder;
using JetBrains.Text;

namespace JetBrains.ReSharper.Plugins.Yaml.Psi.Parsing
{
  internal class YamlTreeStructureBuilder : TreeStructureBuilderBase, IPsiBuilderTokenFactory
  {
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
      // Make sure we don't skip leading whitespace
      var mark = Builder.Mark();

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
      SkipWhitespaces();

      if (Builder.Eof())
        return;

      var mark = Mark();

      ParseDirectives();
      if (GetTokenType() != YamlTokenType.DOCUMENT_END)
        ParseBlockNode(-1, false);

      // TODO: What about indents here?
      while (!Builder.Eof() && GetTokenTypeNoSkipWhitespace() != YamlTokenType.DOCUMENT_END)
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

      var mark = Mark();

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

      var mark = Mark();

      ExpectToken(YamlTokenType.PERCENT);
      SkipSingleLineWhitespace();

      bool parsed;
      do
      {
        parsed = ExpectToken(YamlTokenType.NS_CHARS, dontSkipSpacesAfter: true);
        SkipSingleLineWhitespace();
      } while (parsed && !Builder.Eof() && GetTokenTypeNoSkipWhitespace() != YamlTokenType.NEW_LINE);

      if (!Builder.Eof())
        Advance();  // NEW_LINE

      // Consume following comment lines
      do
      {
        var curr = Builder.GetCurrentLexeme();
        SkipWhitespaces();
        if (GetTokenType() == YamlTokenType.INDENT)
          Builder.AdvanceLexer();
        SkipWhitespaces();
        if (curr == Builder.GetCurrentLexeme())
          break;
      } while (!Builder.Eof() && WhitespacesSkipped);

      Done(mark, ElementType.DIRECTIVE);
    }

    // block-in being "inside a block sequence"
    private void ParseBlockNode(int indent, bool isBlockIn)
    {
      if (!TryParseBlockInBlock(indent, isBlockIn))
        ParseFlowInBlock(indent);
    }

    private bool TryParseBlockInBlock(int indent, bool isBlockIn)
    {
      return TryParseBlockScalar(indent) || TryParseBlockCollection(indent, isBlockIn);
    }

    private bool TryParseBlockScalar(int indent)
    {
      var mark = Mark();

      ParseNodeProperties();

      var tt = GetTokenType();
      if (tt == YamlTokenType.PIPE)
        ParseLiteralScalar(indent, mark);
      else if (tt == YamlTokenType.GT)
        ParseFoldedScalar(indent, mark);
      else
      {
        Builder.RollbackTo(mark);
        return false;
      }

      return true;
    }

    private void ParseLiteralScalar(int indent, int mark)
    {
      ExpectToken(YamlTokenType.PIPE);

      var scalarIndent = ParseBlockHeader(indent);
      ParseMultilineScalar(scalarIndent);

      DoneBeforeWhitespaces(mark, ElementType.LITERAL_SCALAR_NODE);
    }

    private void ParseFoldedScalar(int indent, int mark)
    {
      ExpectToken(YamlTokenType.GT);

      var scalarIndent = ParseBlockHeader(indent);
      ParseMultilineScalar(scalarIndent);

      DoneBeforeWhitespaces(mark, ElementType.FOLDED_SCALAR_NODE);
    }

    private void ParseMultilineScalar(int indent)
    {
      // Keep track of the end of the value. We'll roll back to here at the
      // end. We only update it when we have valid content, or a valid indent
      // If we get something else (new lines or invalid content) we'll advance
      // but not move this forward, giving us somewhere to roll back to
      var endOfValueMark = MarkNoSkipWhitespace();

      // We're interested in NEW_LINE
      SkipSingleLineWhitespace();
      var tt = GetTokenTypeNoSkipWhitespace();
      while (tt == YamlTokenType.SCALAR_TEXT || tt == YamlTokenType.INDENT || tt == YamlTokenType.NEW_LINE)
      {
        // Note that the lexer has handled some indent details for us, too.
        // The lexer will create INDENT tokens of any leading whitespace that
        // is equal or greater to the start of the block scalar. If it matches
        // content before the indent, it doesn't get treated as SCALAR_TEXT
        if (indent == -1 && tt == YamlTokenType.INDENT)
          indent = GetTokenLength();

        if (tt == YamlTokenType.SCALAR_TEXT || (tt == YamlTokenType.INDENT && GetTokenLength() > indent))
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

    private int ParseBlockHeader(int indent)
    {
      var tt = GetTokenType();
      if (tt != YamlTokenType.NS_DEC_DIGIT && tt != YamlTokenType.PLUS && tt != YamlTokenType.MINUS)
        return -1;

      var mark = Mark();

      var relativeIndent = -1;
      if (tt == YamlTokenType.NS_DEC_DIGIT)
      {
        relativeIndent = ParseDecDigit(indent);
        ParseChompingIndicator();
      }
      else
      {
        // We already know it's PLUS or MINUS
        ParseChompingIndicator();
        relativeIndent = ParseDecDigit(indent);
      }

      DoneBeforeWhitespaces(mark, ElementType.BLOCK_HEADER);

      return relativeIndent;
    }

    private int ParseDecDigit(int indent)
    {
      if (GetTokenType() == YamlTokenType.NS_DEC_DIGIT)
      {
        int.TryParse(Builder.GetTokenText(), out var relativeIndent);
        Advance();
        // TODO: Is this correct? It needs to be the indent of its parent node. Is that what we've got here?
        return indent + relativeIndent;
      }

      return -1;
    }

    private void ParseChompingIndicator()
    {
      var tt = GetTokenType();
      if (tt == YamlTokenType.PLUS || tt == YamlTokenType.MINUS)
        Advance();
    }

    private bool TryParseBlockCollection(int indent, bool isBlockIn)
    {
      var mark = Mark();

      ParseNodeProperties();

      var tt = GetTokenType();
      if (tt == YamlTokenType.MINUS)
      {
        // Nested block sequences may be indented one less space, because people
        // intuitively see `-` as indent
        ParseBlockSequence(isBlockIn ? indent - 1 : indent, mark);
      }
      else if (tt == YamlTokenType.QUESTION)
        ParseBlockMapping(indent, mark);
      else
      {
        // TODO: Implicit mapping keys
        Builder.RollbackTo(mark);
        return false;
      }

      return true;
    }

    private void ParseBlockSequence(int indent, int mark)
    {
      do
      {
        ParseBlockSequenceItem(indent);
      } while (!Builder.Eof() && (GetTokenType() == YamlTokenType.MINUS || LookAhead(1) == YamlTokenType.MINUS));

      DoneBeforeWhitespaces(mark, ElementType.BLOCK_SEQUENCE_NODE);
    }

    private void ParseBlockSequenceItem(int indent)
    {
      var mark = Mark();

      if (GetTokenType() == YamlTokenType.INDENT)
      {
        indent += GetTokenLength();
        Advance();
      }

      ExpectToken(YamlTokenType.MINUS);
      ParseBlockNode(indent, true);

      DoneBeforeWhitespaces(mark, ElementType.BLOCK_SEQUENCE_ITEM_NODE);
    }

    private void ParseBlockMapping(int indent, int mark)
    {
      do
      {
        ParseBlockMappingPair(indent);
      } while (!Builder.Eof());

      DoneBeforeWhitespaces(mark, ElementType.BLOCK_MAPPING_NODE);
    }

    private void ParseBlockMappingPair(int indent)
    {
      var mark = Mark();

      if (GetTokenType() == YamlTokenType.INDENT)
      {
        indent += GetTokenLength();
        Advance();
      }

      // TODO: Handle implicit keys
      // TODO: Handle compact notation
      ExpectToken(YamlTokenType.QUESTION);
      ParseBlockNode(indent, false);

      if (indent > 0)
        ExpectToken(YamlTokenType.INDENT);

      if (GetTokenType() == YamlTokenType.COLON)
      {
        ExpectToken(YamlTokenType.COLON);
        ParseBlockNode(indent, false);
      }
      else
      {
        var emptyMark = Mark();
        DoneBeforeWhitespaces(emptyMark, ElementType.EMPTY_SCALAR_NODE);
      }

      DoneBeforeWhitespaces(mark, ElementType.BLOCK_MAPPING_PAIR_NODE);
    }

    private void ParseFlowInBlock(int indent)
    {
      ParseFlowNode(indent + 1);
    }

    private void ParseFlowNode(int indent)
    {
      var tt = GetTokenType();
      if (tt == YamlTokenType.ASTERISK)
        ParseAliasNode();
      else
        ParseFlowContent(indent);
    }

    private void ParseAliasNode()
    {
      var mark = Mark();
      ExpectToken(YamlTokenType.ASTERISK);
      ExpectTokenNoSkipWhitespace(YamlTokenType.NS_ANCHOR_NAME);
      DoneBeforeWhitespaces(mark, ElementType.ALIAS_NODE);
    }

    private void ParseNodeProperties()
    {
      var tt = GetTokenType();
      if (tt != YamlTokenType.BANG && tt != YamlTokenType.BANG_LT && tt != YamlTokenType.AMP)
        return;

      var mark = Mark();

      if (tt == YamlTokenType.BANG || tt == YamlTokenType.BANG_LT)
      {
        ParseTagProperty();
        ParseAnchorProperty();
      }
      else if (tt == YamlTokenType.AMP)
      {
        ParseAnchorProperty();
        ParseTagProperty();
      }

      DoneBeforeWhitespaces(mark, ElementType.NODE_PROPERTIES);
    }

    private void ParseAnchorProperty()
    {
      var tt = GetTokenType();
      if (tt != YamlTokenType.AMP)
        return;

      var mark = Mark();
      ExpectToken(YamlTokenType.AMP);
      ExpectTokenNoSkipWhitespace(YamlTokenType.NS_ANCHOR_NAME);
      DoneBeforeWhitespaces(mark, ElementType.ANCHOR_PROPERTY);
    }

    private void ParseTagProperty()
    {
      var tt = GetTokenType();
      if (tt != YamlTokenType.BANG && tt != YamlTokenType.BANG_LT)
        return;

      if (tt == YamlTokenType.BANG_LT)
      {
        ParseVerbatimTagProperty();
        return;
      }

      // tt == YamlTokenType.BANG
      if (LookAheadNoSkipWhitespaces(1).IsWhitespace)
        ParseNonSpecificTagProperty();
      else
        ParseShorthandTagProperty();
    }

    private void ParseVerbatimTagProperty()
    {
      var mark = Mark();
      ExpectToken(YamlTokenType.BANG_LT);
      ExpectTokenNoSkipWhitespace(YamlTokenType.NS_URI_CHARS);
      ExpectTokenNoSkipWhitespace(YamlTokenType.GT);
      DoneBeforeWhitespaces(mark, ElementType.VERBATIM_TAG_PROPERTY);
    }

    private void ParseShorthandTagProperty()
    {
      var mark = Mark();
      ParseTagHandle();
      var tt = GetTokenTypeNoSkipWhitespace();
      // TODO: Is TAG_CHARS a superset of ns-plain?
      // TODO: Perhaps we should accept all text and add an inspection for invalid chars?
      if (tt != YamlTokenType.NS_TAG_CHARS && tt != YamlTokenType.NS_PLAIN_ONE_LINE)
        ErrorBeforeWhitespaces(ParserMessages.GetExpectedMessage("text"));
      Advance();
      DoneBeforeWhitespaces(mark, ElementType.SHORTHAND_TAG_PROPERTY);
    }

    private void ParseTagHandle()
    {
      var mark = Mark();
      ExpectToken(YamlTokenType.BANG);
      var elementType = ParseSecondaryOrNamedTagHandle();
      DoneBeforeWhitespaces(mark, elementType);
    }

    private CompositeNodeType ParseSecondaryOrNamedTagHandle()
    {
      // Make sure we don't try to match a primary tag handle followed by ns-plain. E.g. `!foo`
      var tt = GetTokenTypeNoSkipWhitespace();
      var la = LookAhead(1);
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

    private void ParseNonSpecificTagProperty()
    {
      var mark = Mark();
      ExpectToken(YamlTokenType.BANG);
      DoneBeforeWhitespaces(mark, ElementType.NON_SPECIFIC_TAG_PROPERTY);
    }

    private void ParseFlowContent(int indent)
    {
      var mark = Mark();

      ParseNodeProperties();

      CompositeNodeType elementType;



      if (GetTokenType() == YamlTokenType.INDENT)
        Advance();




      var tt = GetTokenType();
      if (tt == YamlTokenType.LBRACK)
        elementType = ParseFlowSequence(indent);
      else if (tt == YamlTokenType.LBRACE)
        elementType = ParseFlowMapping(indent);
      else if (tt == YamlTokenType.C_DOUBLE_QUOTED_MULTI_LINE || tt == YamlTokenType.C_DOUBLE_QUOTED_SINGLE_LINE)
      {
        Advance();
        elementType = ElementType.DOUBLE_QUOTED_SCALAR_NODE;
      }
      else if (tt == YamlTokenType.C_SINGLE_QUOTED_MULTI_LINE || tt == YamlTokenType.C_SINGLE_QUOTED_SINGLE_LINE)
      {
        Advance();
        elementType = ElementType.SINGLE_QUOTED_SCALAR_NODE;
      }
      else if (tt == YamlTokenType.NS_PLAIN_MULTI_LINE || tt == YamlTokenType.NS_PLAIN_ONE_LINE)
      {
        // TODO: Multi-line and indents. Woohoo!
        Advance();
        elementType = ElementType.PLAIN_SCALAR_NODE;
      }
      else
        elementType = ElementType.EMPTY_SCALAR_NODE;

      DoneBeforeWhitespaces(mark, elementType);
    }

    private CompositeNodeType ParseFlowSequence(int indent)
    {
      ExpectToken(YamlTokenType.LBRACK);

      ParseFlowSequenceEntry(indent);
      if (GetTokenType() == YamlTokenType.COMMA)
      {
        do
        {
          ExpectToken(YamlTokenType.COMMA);
          if (GetTokenType() != YamlTokenType.RBRACK)
            ParseFlowSequenceEntry(indent);
        } while (!Builder.Eof() && GetTokenType() != YamlTokenType.RBRACK && GetTokenType() == YamlTokenType.COMMA);
      }

      while (GetTokenType() != YamlTokenType.RBRACK)
        Advance();
      ExpectToken(YamlTokenType.RBRACK);

      return ElementType.FLOW_SEQUENCE_NODE;
    }

    private void ParseFlowSequenceEntry(int indent)
    {
      if (!TryParseFlowPair(indent))
        ParseFlowNode(indent);
    }

    private bool TryParseFlowPair(int indent)
    {
      // TODO: Compact flow pair notation
      return false;
    }

    private CompositeNodeType ParseFlowMapping(int indent)
    {
      ExpectToken(YamlTokenType.LBRACE);

      ParseFlowMapEntry();
      if (GetTokenType() == YamlTokenType.COMMA)
      {
        do
        {
          ExpectToken(YamlTokenType.COMMA);
          if (GetTokenType() != YamlTokenType.RBRACE)
            ParseFlowMapEntry();
        } while (!Builder.Eof() && GetTokenType() != YamlTokenType.RBRACE);
      }

      while (GetTokenType() != YamlTokenType.RBRACE)
        Advance();
      ExpectToken(YamlTokenType.RBRACE);

      return ElementType.FLOW_MAPPING_NODE;
    }

    private void ParseFlowMapEntry()
    {
      // TODO: Parse flow map entry
      while (!Builder.Eof() && GetTokenType() != YamlTokenType.COMMA && GetTokenType() != YamlTokenType.RBRACE)
        Advance();
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

    private int MarkNoSkipWhitespace()
    {
      // this.Mark() calls SkipWhitespace() first
      return Builder.Mark();
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
  }
}