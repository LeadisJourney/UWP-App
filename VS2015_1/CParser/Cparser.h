#pragma once

namespace Pfm = ::Platform;

namespace Cparser
{
  public enum class TokenType
  {
    None
  };

  public value struct Token sealed
  {
    uint16 begin;
    uint16 end;
    TokenType type;
  };

  public delegate void NewTokenParsed(Token token);

  public ref class CParser sealed
  {
    std::vector<std::pair<wchar_t, int>> text;
    ~CParser();
  public:
    CParser(Pfm::String ^text);
    event NewTokenParsed ^NewTokenParsed;
  };
}
