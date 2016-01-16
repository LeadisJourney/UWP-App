namespace Leadis_Journey

type TokenType =
  | None      = 0
  | Keyword   = 1
  | Directive = 2
  | LitNumber = 3
  | LitString = 4
  | Macro     = 5
  | Comment   = 10

type Token =
  struct
    val Type : TokenType
    val Start : int
    val End : int
    new(type':TokenType, start':int, end':int) =
      { Type=type'; Start=start'; End=end' }
  end
