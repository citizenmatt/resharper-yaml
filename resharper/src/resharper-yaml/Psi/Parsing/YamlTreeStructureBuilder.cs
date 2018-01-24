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

      var mark = Builder.Mark();

      ParseDirectives();

      // PARSE REST
      do
      {
        if (!Builder.Eof())
          Advance();

      } while (!Builder.Eof() && GetTokenType() != YamlTokenType.DOCUMENT_END);
      // PARSE REST

      if (!Builder.Eof())
        ExpectToken(YamlTokenType.DOCUMENT_END);

      Done(mark, ElementType.YAML_DOCUMENT);
    }

    private void ParseDirectives()
    {
      var tt = GetTokenType();
      if (tt != YamlTokenType.PERCENT && tt != YamlTokenType.DIRECTIVES_END)
        return;

      var mark = Builder.Mark();

      do
      {
        var curr = Builder.GetCurrentLexeme();
        ParseDirective();
        if (curr == Builder.GetCurrentLexeme())
          break;
      } while (!Builder.Eof() && Builder.GetTokenType() != YamlTokenType.DIRECTIVES_END);

      if (!Builder.Eof())
        ExpectToken(YamlTokenType.DIRECTIVES_END);

      DoneBeforeWhitespaces(mark, ElementType.DIRECTIVES);
    }

    private void ParseDirective()
    {
      if (GetTokenType() != YamlTokenType.PERCENT)
        return;

      var mark = Builder.Mark();

      ExpectToken(YamlTokenType.PERCENT);
      SkipSingleLineWhitespace();

      bool parsed;
      do
      {
        parsed = ExpectToken(YamlTokenType.NS_CHARS, dontSkipSpacesAfter: true);
        SkipSingleLineWhitespace();
      } while (parsed && !Builder.Eof() && Builder.GetTokenType() != YamlTokenType.NEW_LINE);

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

    private void SkipSingleLineWhitespace()
    {
      while (!Builder.Eof())
      {
        var tt = Builder.GetTokenType();
        if (tt == YamlTokenType.NEW_LINE || (!tt.IsWhitespace && !tt.IsComment))
          return;

        Advance();
      }
    }
  }
}