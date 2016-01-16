namespace Leadis_Journey

type TokenType =
  | None      = 0b00000000
  | Keyword   = 0b00000001
  | Directive = 0b00000010
  | LitString = 0b00000011

type Token =
  struct
    val Type : TokenType
    val Start : int
    val End : int
    new(type':TokenType, start':int, end':int) =
      { Type=type'; Start=start'; End=end' }
  end
