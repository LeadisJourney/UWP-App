namespace Leadis_Journey

open FParsec
open Microsoft.FSharp.Collections
open System
open System.Collections.Generic

module private _Private =

  let keywords = Set([|"auto"    ;"_Bool"     ;"break"   ;"case"    ;"char"   ;
                       "_Complex";"const"     ;"continue";"default" ;"do"     ;
                       "double"  ;"extern"    ;"float"   ;"for"     ;"goto"   ;
                       "if"      ;"_Imaginary";"inline"  ;"int"     ;"long"   ;
                       "register";"restrict"  ;"return"  ;"short"   ;"signed" ;
                       "sizeof"  ;"static"    ;"struct"  ;"switch"  ;"typedef";
                       "union"   ;"unsigned"  ;"void"    ;"volatile";"while"|])

  let directives = Set([|"define";"error";"import";"undef";"elif" ;"if"    ;"include";
                         "using" ;"else" ;"ifdef" ;"line" ;"endif";"ifndef";"pragma"|])

  let pstart, _pstart = createParserForwardedToRef()

  let parserToToken parser' ftoken' =
                pipe3 getPosition
                      parser'
                      getPosition
                      (fun start' res' end' -> let starti = int start'.Index
                                               let endi = int end'.Index
                                               ftoken' res' starti endi)
                >>= fun token -> updateUserState (fun (state':List<Token>) -> state'.Add(token);state')

  let pidentifier = let isAsciiIdStart c = isAsciiLetter c || c = '_' in
                    let isAsciiIdContinue c = isAsciiIdStart c || isDigit c in
                    identifier (IdentifierOptions(isAsciiIdStart=isAsciiIdStart,
                                                  isAsciiIdContinue=isAsciiIdContinue)) in

  let pidentifierToken = parserToToken pidentifier
                                       (fun id' start' end' -> if keywords.Contains(id')
                                                               then Token(TokenType.Keyword, start', end')
                                                               else Token())

  let pdirective = skipChar '#'
                   >>. skipMany spaces1
                   >>. opt pidentifier
                   .>> spaces
                   .>> (skipUnicodeNewline <|> eof)

  let pdirectiveToken = parserToToken pdirective
                                      (fun id' start' end' -> if id'.IsNone || directives.Contains(id'.Value)
                                                              then Token(TokenType.Directive, start', end')
                                                              else Token())

  let pnumber = let options = NumberLiteralOptions.AllowFraction
                              ||| NumberLiteralOptions.AllowFractionWOIntegerPart
                              ||| NumberLiteralOptions.AllowExponent
                              ||| NumberLiteralOptions.AllowHexadecimal
                              ||| NumberLiteralOptions.AllowExponent
                              ||| NumberLiteralOptions.AllowMinusSign
                              ||| NumberLiteralOptions.AllowPlusSign in
                numberLiteralE options (otherError null)

  let pnumberToken = parserToToken pnumber (fun _ start' end' -> Token(TokenType.LitNumber, start', end'))

  let plitstring = let normalChar = skipMany1Satisfy (fun c' -> c' <> '\\' && c' <> '"')
                   let escapedChar = skipString "\\\""
                   between (skipChar '"') (skipChar '"') (skipMany (normalChar <|> escapedChar))

  let plitstringToken = parserToToken plitstring (fun _ start' end' -> Token(TokenType.LitString, start', end'))

  let plexeme = choice [|pidentifierToken;
                         pdirectiveToken;
                         pnumberToken;
                         plitstringToken|]

  do _pstart :=  spaces
                 >>. skipMany (plexeme >>. spaces)

open _Private

type CParser() =

  member xx.Parse(text':string) =
    match runParserOnString pstart (List<Token>()) "" text' with
    | Success(_, state, _)
    | Failure(_, _, state) -> state
