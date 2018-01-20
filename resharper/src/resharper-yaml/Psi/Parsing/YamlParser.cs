using System.Text;
using JetBrains.Annotations;
using JetBrains.Application.Threading;
using JetBrains.ReSharper.Plugins.Yaml.Psi.Gen;
using JetBrains.ReSharper.Plugins.Yaml.Psi.Tree.Impl;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Util;
using JetBrains.Text;
using JetBrains.Util;

namespace JetBrains.ReSharper.Plugins.Yaml.Psi.Parsing
{
  internal class YamlParser : YamlParserGenerated, IYamlParser
  {
    [NotNull] private readonly ILexer<int> myOriginalLexer;
    private readonly CommonIdentifierIntern myIntern;
    private ITokenIntern myTokenIntern;

    public YamlParser([NotNull] ILexer<int> lexer, CommonIdentifierIntern intern)
    {
      myOriginalLexer = lexer;
      myIntern = intern;

      SetLexer(new YamlFilteringLexer(lexer));
    }

    public ITokenIntern TokenIntern => myTokenIntern ?? (myTokenIntern = new LexerTokenIntern(10));

    public IFile ParseFile()
    {
      return myIntern.DoWithIdentifierIntern(intern =>
      {
        var element = ParseYamlFile();
        InsertMissingTokens(element, intern);
        return (IFile) element;
      });
    }

    protected override TreeElement CreateToken()
    {
      var tokenType = myLexer.TokenType;

      Assertion.Assert(tokenType != null, "tokenType != null");

      // Node offsets aren't stored during parsing. However, we need the absolute file
      // offset position so we can re-insert filtered tokens, so call SetOffset here.
      // Implementation details: This is non-obvious, so I'm going into implementation
      // details. SetOffset updates TreeElement.myCachedOffsetData to an absolute offset,
      // indicated by having a negative value, offset by 2. In other words, -1 means unset,
      // -2 means 0 and -3 means an absolute offset of 1. This offset is only valid during
      // parsing! After parsing, TreeElement.myCachedOffsetData is re-used to cache the offset
      // (relative to the parent node) calculated from a call to GetTextLength or GetTreeStartOffset
      // and invalidated by SubTreeChanged, or otherwise modifying the tree. The relative
      // offset is indicated by a positive value, or 0. The implementation of
      // MissingTokenInserterBase.GetLeafOffset has a minor optimisation that tries to downcast
      // the leaf element to BindedToBufferLeafElement, and uses the Offset property there.
      // If all tokens inherit from BindedToBufferLeafElement, the offset is known at parse
      // time, and we don't need to call SetOffset here (doing so is ignored)
      var tokenStart = myLexer.TokenStart;
      var element = CreateToken(tokenType);
      if (element is LeafElementBase leaf)
          SetOffset(leaf, tokenStart);
      return element;
    }

    private TreeElement CreateToken([NotNull] TokenNodeType tokenType)
    {
      LeafElementBase element;
      if (tokenType == YamlTokenType.NS_ANCHOR_NAME
          || tokenType == YamlTokenType.NS_CHARS
          || tokenType == YamlTokenType.NS_PLAIN_ONE_LINE
          || tokenType == YamlTokenType.NS_TAG_CHARS
          || tokenType == YamlTokenType.NS_URI_CHARS
          || tokenType == YamlTokenType.NS_WORD_CHARS
          || tokenType == YamlTokenType.SCALAR_TEXT
          || tokenType == YamlTokenType.C_DOUBLE_QUOTED_SINGLE_LINE
          || tokenType == YamlTokenType.C_SINGLE_QUOTED_SINGLE_LINE)
      {
        // Interning the token text will allow us to reuse existing string instances.
        // The interner will look up data directly from the lexer, without allocating
        // a string first. This only allocates if the string has not already been interned.
        var text = TokenIntern.Intern(myLexer);
        element = tokenType.Create(text);
      }
      else
      {
        element = tokenType.Create(myLexer.Buffer, new TreeOffset(myLexer.TokenStart),
          new TreeOffset(myLexer.TokenEnd));
      }

      myLexer.Advance();

      return element;
    }

    private void InsertMissingTokens(TreeElement root, ITokenIntern intern)
    {
      var interruptChecker = new SeldomInterruptChecker();
      YamlMissingTokensInserter.Run(root, myOriginalLexer, this, interruptChecker, intern);
    }

    public override TreeElement ParseUnparsedError()
    {
      throw new SyntaxError("Not yet handled");
    }
  }

  internal class YamlMissingTokensInserter : MissingTokenInserterBase
  {
    private readonly ILexer myLexer;

    private YamlMissingTokensInserter(ILexer lexer, ITokenOffsetProvider offsetProvider,
                                      SeldomInterruptChecker interruptChecker, ITokenIntern intern)
      : base(offsetProvider, interruptChecker, intern)
    {
      myLexer = lexer;
    }

    protected override void ProcessLeafElement(TreeElement leafElement)
    {
      var leafOffset = GetLeafOffset(leafElement).Offset;

      if (myLexer.TokenType != null && myLexer.TokenStart < leafOffset)
      {
        var anchor = leafElement;
        var parent = anchor.parent;
        while (anchor == parent.FirstChild && parent.parent != null)
        {
          anchor = parent;
          parent = parent.parent;
        }

        while (myLexer.TokenType != null && myLexer.TokenStart < leafOffset)
        {
          var token = CreateMissingToken();

          parent.AddChildBefore(token, anchor);

          var skipTo = myLexer.TokenStart + token.GetTextLength();
          while (myLexer.TokenType != null && myLexer.TokenStart < skipTo)
            myLexer.Advance();
        }
      }

      var leafEndOffset = leafOffset + leafElement.GetTextLength();
      while (myLexer.TokenType != null && myLexer.TokenStart < leafEndOffset)
        myLexer.Advance();
    }

    private TreeElement CreateMissingToken()
    {
      var tokenType = myLexer.TokenType;
      if (tokenType == YamlTokenType.WHITESPACE)
        return new Whitespace(myWhitespaceIntern.Intern(myLexer));
      if (tokenType == YamlTokenType.NEW_LINE)
        return new NewLine(myWhitespaceIntern.Intern(myLexer));
      return TreeElementFactory.CreateLeafElement(myLexer);
    }

    public static void Run(TreeElement node, ILexer lexer, ITokenOffsetProvider offsetProvider,
      SeldomInterruptChecker interruptChecker, ITokenIntern intern)
    {
      Assertion.Assert(node.parent == null, "node.parent == null");

      if (!(node is CompositeElement root))
        return;

      // Append an EOF token so we insert filtered tokens right up to the end of file
      var eof = new EofToken(lexer.Buffer.Length);
      root.AppendNewChild(eof);

      var inserter = new YamlMissingTokensInserter(lexer, offsetProvider, interruptChecker, intern);

      // Reset the lexer, walk the tree and call ProcessLeafElement on each leaf element
      lexer.Start();
      inserter.Run(root);

      root.DeleteChildRange(eof, eof);
    }

    private class EofToken : LeafElementBaseWithCustomOffset
    {
      public EofToken(int position)
        : base(new TreeOffset(position))
      {
      }

      public override int GetTextLength() => 0;
      public override StringBuilder GetText(StringBuilder to) => to;
      public override IBuffer GetTextAsBuffer() => new StringBuffer(string.Empty);
      public override string GetText() => string.Empty;
      public override NodeType NodeType => YamlTokenType.EOF;
      // ReSharper disable once AssignNullToNotNullAttribute
      public override PsiLanguageType Language => YamlLanguage.Instance;
    }
  }
}