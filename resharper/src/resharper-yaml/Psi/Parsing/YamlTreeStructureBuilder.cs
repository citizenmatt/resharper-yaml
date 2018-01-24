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
        SkipWhitespaces();
        ParseDocument();
      } while (!Builder.Eof());

      Done(mark, ElementType.YAML_FILE);
    }

    private void ParseDocument()
    {
      if (Builder.Eof())
        return;

      // Make sure we don't skip leading whitespace
      var mark = Builder.Mark();

      ParseDirectives();
      if (GetTokenType() != YamlTokenType.DOCUMENT_END)
        ParseBlockNode();

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

    private void ParseBlockNode()
    {
      if (!TryParseBlockInBlock())
        ParseFlowInBlock();
    }

    private bool TryParseBlockInBlock()
    {
      return TryParseBlockScalar() || TryParseBlockCollection();
    }

    private bool TryParseBlockScalar()
    {
      var mark = Mark();

      ParseNodeProperties();

      var tt = GetTokenType();
      if (tt != YamlTokenType.PIPE && tt != YamlTokenType.GT)
      {
        Builder.RollbackTo(mark);
        return false;
      }

      if (tt == YamlTokenType.PIPE)
        ParseLiteralScalar(mark);
      if (tt == YamlTokenType.GT)
        ParseFoldedScalar(mark);

      return true;
    }

    private void ParseLiteralScalar(int mark)
    {
      ExpectToken(YamlTokenType.PIPE);
      ParseBlockHeader();

      // TODO: Proper indent handling
      do
      {
        if (GetTokenType() == YamlTokenType.INDENT)
          Advance();
        ExpectToken(YamlTokenType.SCALAR_TEXT);
      } while (!Builder.Eof() && (GetTokenType() == YamlTokenType.INDENT || GetTokenType() == YamlTokenType.SCALAR_TEXT));

      DoneBeforeWhitespaces(mark, ElementType.LITERAL_SCALAR_NODE);
    }

    private void ParseFoldedScalar(int mark)
    {
      ExpectToken(YamlTokenType.GT);
      ParseBlockHeader();

      // TODO: Proper indent handling
      do
      {
        do
        {
          if (GetTokenType() == YamlTokenType.INDENT)
            Advance();
        } while (!Builder.Eof() && GetTokenType() == YamlTokenType.INDENT);

        if (GetTokenType() == YamlTokenType.SCALAR_TEXT)
          Advance();

      } while (!Builder.Eof() && (GetTokenType() == YamlTokenType.INDENT || GetTokenType() == YamlTokenType.SCALAR_TEXT));

      DoneBeforeWhitespaces(mark, ElementType.FOLDED_SCALAR_NODE);
    }

    private void ParseBlockHeader()
    {
      var tt = GetTokenType();
      if (tt != YamlTokenType.NS_DEC_DIGIT && tt != YamlTokenType.PLUS && tt != YamlTokenType.MINUS)
        return;

      var mark = Mark();
      if (tt == YamlTokenType.NS_DEC_DIGIT)
      {
        ExpectToken(YamlTokenType.NS_DEC_DIGIT);
        tt = GetTokenType();
        if (tt == YamlTokenType.PLUS || tt == YamlTokenType.MINUS)
          Advance();
      }
      else
      {
        Advance();  // PLUS or MINUS
        if (GetTokenTypeNoSkipWhitespace() == YamlTokenType.NS_DEC_DIGIT)
          Advance();
      }
      DoneBeforeWhitespaces(mark, ElementType.BLOCK_HEADER);
    }

    private bool TryParseBlockCollection()
    {
      var mark = Mark();

      ParseNodeProperties();

      var tt = GetTokenType();
      if (tt == YamlTokenType.MINUS)
        ParseBlockSequence(mark);
      else if (tt == YamlTokenType.QUESTION)
        ParseBlockMapping(mark);
      else
      {
        // TODO: Implicit mapping keys
        Builder.RollbackTo(mark);
        return false;
      }

      return true;
    }

    private void ParseBlockSequence(int mark)
    {
      do
      {
        ParseBlockSequenceItem();
      } while (!Builder.Eof() && (GetTokenType() == YamlTokenType.MINUS || LookAhead(1) == YamlTokenType.MINUS));
      DoneBeforeWhitespaces(mark, ElementType.BLOCK_SEQUENCE_NODE);
    }

    private void ParseBlockSequenceItem()
    {
      var mark = Mark();

      // TODO: Proper indent handling!
      if (GetTokenType() == YamlTokenType.INDENT)
        Advance();
      ExpectToken(YamlTokenType.MINUS);
      ParseBlockNode();

      DoneBeforeWhitespaces(mark, ElementType.BLOCK_SEQUENCE_ITEM_NODE);
    }

    private void ParseBlockMapping(int mark)
    {
      do
      {
        ParseBlockMappingPair();
      } while (!Builder.Eof());
      DoneBeforeWhitespaces(mark, ElementType.BLOCK_MAPPING_NODE);
    }

    private void ParseBlockMappingPair()
    {
      var mark = Mark();

      // TODO: Proper indent handling!
      if (GetTokenType() == YamlTokenType.INDENT)
        Advance();
      ExpectToken(YamlTokenType.QUESTION);
      ParseBlockNode();
      if (GetTokenType() == YamlTokenType.COLON)
      {
        ExpectToken(YamlTokenType.COLON);
        ParseBlockNode();
      }
      else
      {
        var emptyMark = Mark();
        DoneBeforeWhitespaces(emptyMark, ElementType.EMPTY_SCALAR_NODE);
      }

      DoneBeforeWhitespaces(mark, ElementType.BLOCK_MAPPING_PAIR_NODE);
    }

    private void ParseFlowInBlock()
    {
      ParseFlowNode();
    }

    private void ParseFlowNode()
    {
      var tt = GetTokenType();
      if (tt == YamlTokenType.ASTERISK)
        ParseAliasNode();
      else
      {
        var mark = Mark();
        ParseNodeProperties();
        ParseFlowContent(mark);
      }
    }

    private void ParseAliasNode()
    {
      var mark = Mark();
      ExpectToken(YamlTokenType.ASTERISK);
      ExpectToken(YamlTokenType.NS_ANCHOR_NAME);
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

    private void ParseFlowContent(int mark)
    {
      CompositeNodeType elementType;

      // TODO: Proper handling of indents!
      if (GetTokenType() == YamlTokenType.INDENT)
        Advance();

      var tt = GetTokenType();
      if (tt == YamlTokenType.LBRACK)
        elementType = ParseFlowSequence();
      else if (tt == YamlTokenType.LBRACE)
        elementType = ParseFlowMapping();
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

    private CompositeNodeType ParseFlowSequence()
    {
      do
      {
        Advance();
      } while (!Builder.Eof() && GetTokenType() != YamlTokenType.RBRACK);
      Advance();  // RBRACK

      return ElementType.FLOW_SEQUENCE_NODE;
    }

    private CompositeNodeType ParseFlowMapping()
    {
      do
      {
        Advance();
      } while (!Builder.Eof() && GetTokenType() != YamlTokenType.RBRACE);
      Advance();  // RBRACE

      return ElementType.FLOW_MAPPING_NODE;
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