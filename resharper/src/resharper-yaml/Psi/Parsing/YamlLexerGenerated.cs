using System;
using System.Text.RegularExpressions;
using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.Text;
using JetBrains.Util;

namespace JetBrains.ReSharper.Plugins.Yaml.Psi.Parsing
{
  public partial class YamlLexerGenerated
  {
    // ReSharper disable InconsistentNaming
    private TokenNodeType currentTokenType;

    private int currentLineIndent;

    // The indent of the indicator of a block scalar. The contents must be more
    // indented than this. However, we also need to handle the case where the
    // indicator is at column 0, but we're in `block-in` context, i.e. at the
    // root of the doucment. This allows the indent to also be at column 0 (the
    // parent node is treated as being at column -1)
    // TODO: Use the indentation indicator value to set this
    private int blockScalarIndicatorIndent;
    private int blockScalarIndent;

    private struct TokenPosition
    {
      public TokenNodeType CurrentTokenType;
      public int CurrentLineIndent;
      public int BlockScalarIndicatorIndent;
      public int BlockScalarIndent;
      public int YyBufferIndex;
      public int YyBufferStart;
      public int YyBufferEnd;
      public int YyLexicalState;
    }
    // ReSharper restore InconsistentNaming

    public void Start()
    {
      Start(0, yy_buffer.Length, YYINITIAL);
    }

    public void Start(int startOffset, int endOffset, uint state)
    {
      yy_buffer_index = yy_buffer_start = yy_buffer_end = startOffset;
      yy_eof_pos = endOffset;
      yy_lexical_state = (int) state;
      currentTokenType = null;
    }

    public void Advance()
    {
      LocateToken();
      currentTokenType = null;
    }

    public object CurrentPosition
    {
      get
      {
        TokenPosition tokenPosition;
        tokenPosition.CurrentTokenType = currentTokenType;
        tokenPosition.CurrentLineIndent = currentLineIndent;
        tokenPosition.BlockScalarIndicatorIndent = blockScalarIndicatorIndent;
        tokenPosition.BlockScalarIndent = blockScalarIndent;
        tokenPosition.YyBufferIndex = yy_buffer_index;
        tokenPosition.YyBufferStart = yy_buffer_start;
        tokenPosition.YyBufferEnd = yy_buffer_end;
        tokenPosition.YyLexicalState = yy_lexical_state;
        return tokenPosition;
      }
      set
      {
        var tokenPosition = (TokenPosition) value;
        currentTokenType = tokenPosition.CurrentTokenType;
        currentLineIndent = tokenPosition.CurrentLineIndent;
        blockScalarIndicatorIndent = tokenPosition.BlockScalarIndicatorIndent;
        blockScalarIndent = tokenPosition.BlockScalarIndent;
        yy_buffer_index = tokenPosition.YyBufferIndex;
        yy_buffer_start = tokenPosition.YyBufferStart;
        yy_buffer_end = tokenPosition.YyBufferEnd;
        yy_lexical_state = tokenPosition.YyLexicalState;
      }
    }

    public TokenNodeType TokenType => LocateToken();

    public int TokenStart
    {
      get
      {
        LocateToken();
        return yy_buffer_start;
      }
    }

    public int TokenEnd
    {
      get
      {
        LocateToken();
        return yy_buffer_end;
      }
    }

    public IBuffer Buffer => yy_buffer;
    public uint LexerStateEx => (uint) yy_lexical_state;

    public int EOFPos => yy_eof_pos;
    public int LexemIndent => 7;  // No, I don't know why

    protected void RewindToken()
    {
      yy_buffer_end = yy_buffer_index = yy_buffer_start;
    }

    private TokenNodeType LocateToken()
    {
      if (currentTokenType == null)
      {
        try
        {
          currentTokenType = _locateToken();
        }
        catch (Exception e)
        {
          e.AddData("TokenType", () => currentTokenType);
          e.AddData("LexerState", () => LexerStateEx);
          e.AddData("TokenStart", () => yy_buffer_start);
          e.AddData("TokenPos", () => yy_buffer_index);
          e.AddData("Buffer", () =>
          {
            var start = Math.Max(0, yy_buffer_end);
            var tokenText = yy_buffer.GetText(new TextRange(start, yy_buffer_index));
            tokenText = Regex.Replace(tokenText, @"\p{Cc}", a => $"[{(byte) a.Value[0]:X2}]");
            return tokenText;
          });
          throw;
        }
      }

      return currentTokenType;
    }

    private void BeginBlockScalar()
    {
      blockScalarIndicatorIndent = currentLineIndent;
      blockScalarIndent = -1;
      yybegin(BLOCK_SCALAR_HEADER);
    }

    private void HandleBlockScalarWhitespace()
    {
      // If the content indent hasn't been set, and we're indented in relation to the
      // indicator, indent the content, otherwise, terminate the block scalar
      if (blockScalarIndent == -1 && currentLineIndent >= blockScalarIndicatorIndent)
        blockScalarIndent = currentLineIndent;
      else if (currentLineIndent <= blockScalarIndent)
        yybegin(BLOCK_IN);
    }

    private TokenNodeType HandleBlockScalarLine()
    {
      if (currentLineIndent <= blockScalarIndent)
      {
        EndBlockScalar();
        RewindToken();
        return _locateToken();
      }

      return YamlTokenType.SCALAR_TEXT;
    }

    private void EndBlockScalar()
    {
      blockScalarIndent = 0;
      yybegin(BLOCK_IN);
    }
  }
}