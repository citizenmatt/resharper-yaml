using JetBrains.Util;

namespace JetBrains.ReSharper.Plugins.Yaml.Psi.Parsing
{
  public static class ParserMessages
  {
    public static string GetString(string id) => id;

    public static string GetUnexpectedTokenMessage() => "Unexpected token";

    public static string GetExpectedMessage(string expectedSymbol)
    {
      return string.Format(GetString("{0} expected"), expectedSymbol).Capitalize();
    }
  }
}