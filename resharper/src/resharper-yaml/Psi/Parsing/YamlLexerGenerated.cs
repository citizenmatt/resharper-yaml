﻿using System;
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

    private struct TokenPosition
    {
      public TokenNodeType CurrentTokenType;
      public int CurrentLineIndent;
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
  }
}