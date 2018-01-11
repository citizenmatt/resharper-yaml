using JetBrains.ReSharper.Psi.Parsing;

namespace JetBrains.ReSharper.Plugins.Yaml.Psi.Parsing
{
  public static partial class YamlTokenType
  {
    public static readonly TokenNodeType BAD_CHARACTER = new GenericTokenNodeType("BAD_CHARACTER", LAST_GENERATED_TOKEN_TYPE_INDEX + 10, "�");

    public static readonly TokenNodeType EOF = new GenericTokenNodeType("EOF", LAST_GENERATED_TOKEN_TYPE_INDEX + 11, "EOF");

    public static readonly TokenNodeType NEW_LINE = new NewLineNodeType(LAST_GENERATED_TOKEN_TYPE_INDEX + 12);
    public static readonly TokenNodeType WHITESPACE = new WhitespaceNodeType(LAST_GENERATED_TOKEN_TYPE_INDEX + 13);

    public static readonly TokenNodeType COMMENT = new CommentTokenNodeType(LAST_GENERATED_TOKEN_TYPE_INDEX + 14);

    public static readonly TokenNodeType SYNTHETIC_DIRECTIVES_END = new GenericTokenNodeType("SYNTHETIC_DIRECTIVES_END", LAST_GENERATED_TOKEN_TYPE_INDEX + 15, "SYNTHETIC_DIRECTIVES_END");

    // TODO: Naming. The YAML spec has an interesting hungarian notation style...
    // Should NS_URI_CHARS, NS_TAG_CHARS, NS_PLAIN and NS_ANCHOR_NAME just become some kind of "VALUE"?
    // I don't think the parser would really care what type of textual value it is - as long as the value is there
    public static readonly TokenNodeType NS_CHARS = new GenericTokenNodeType("NS_CHARS", LAST_GENERATED_TOKEN_TYPE_INDEX + 20, "NS_CHARS");
    public static readonly TokenNodeType NS_WORD_CHARS = new GenericTokenNodeType("NS_WORD_CHARS", LAST_GENERATED_TOKEN_TYPE_INDEX + 21, "NS_WORD_CHARS");
    public static readonly TokenNodeType NS_URI_CHARS = new GenericTokenNodeType("NS_URI_CHARS", LAST_GENERATED_TOKEN_TYPE_INDEX + 22, "NS_URI_CHARS");
    public static readonly TokenNodeType NS_TAG_CHARS = new GenericTokenNodeType("NS_TAG_CHARS", LAST_GENERATED_TOKEN_TYPE_INDEX + 23, "NS_TAG_CHARS");
    public static readonly TokenNodeType NS_PLAIN = new GenericTokenNodeType("NS_PLAIN", LAST_GENERATED_TOKEN_TYPE_INDEX + 24, "NS_PLAIN");
    public static readonly TokenNodeType NS_ANCHOR_NAME = new GenericTokenNodeType("NS_ANCHOR_NAME", LAST_GENERATED_TOKEN_TYPE_INDEX + 25, "NS_ANCHOR_NAME");
    public static readonly TokenNodeType C_SINGLE_QUOTED_SINGLE_LINE = new GenericTokenNodeType("C_SINGLE_QUOTED_SINGLE_LINE", LAST_GENERATED_TOKEN_TYPE_INDEX + 26, "C_SINGLE_QUOTED_SINGLE_LINE");
    public static readonly TokenNodeType C_SINGLE_QUOTED_MULTILINE = new GenericTokenNodeType("C_SINGLE_QUOTED_MULTILINE", LAST_GENERATED_TOKEN_TYPE_INDEX + 27, "C_SINGLE_QUOTED_MULTILINE");
    public static readonly TokenNodeType C_DOUBLE_QUOTED_SINGLE_LINE = new GenericTokenNodeType("C_DOUBLE_QUOTED_SINGLE_LINE", LAST_GENERATED_TOKEN_TYPE_INDEX + 28, "C_DOUBLE_QUOTED_SINGLE_LINE");
    public static readonly TokenNodeType C_DOUBLE_QUOTED_MULTILINE = new GenericTokenNodeType("C_DOUBLE_QUOTED_MULTILINE", LAST_GENERATED_TOKEN_TYPE_INDEX + 29, "C_DOUBLE_QUOTED_MULTILINE");
  }
}
