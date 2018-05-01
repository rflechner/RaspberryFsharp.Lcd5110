(*
  This code is inspired by the Adafruit library
  https://github.com/adafruit/Adafruit_Nokia_LCD/blob/master/Adafruit_Nokia_LCD/PCD8544.py
*)

module NokiaLcd

open Unosquare.RaspberryIO
open Unosquare.RaspberryIO.Gpio
open System
open System.Threading
open System.Drawing
open System.Drawing.Drawing2D
open System.IO

let LCDWIDTH = 84
let LCDHEIGHT = 48
let private PCD8544_EXTENDEDINSTRUCTION = 0x01uy
let private PCD8544_DISPLAYNORMAL = 0x4uy
let private PCD8544_FUNCTIONSET = 0x20uy
let private PCD8544_DISPLAYCONTROL = 0x08uy
let private PCD8544_SETYADDR = 0x40uy
let private PCD8544_SETXADDR = 0x80uy
let private PCD8544_SETBIAS = 0x10uy
let private PCD8544_SETVOP = 0x80uy

type ScreenContext =
  { Dc:GpioPin
    Rst:GpioPin
    Spi:SpiChannel
    Buffer:byte array ref }
  static member Default =
    let buffer:byte array = Array.zeroCreate (LCDWIDTH * LCDHEIGHT / 8)
    { Dc = Pi.Gpio.Pin23
      Rst = Pi.Gpio.Pin24
      Spi = Pi.Spi.Channel0
      Buffer = ref buffer }

let sleep n =
  n |> TimeSpan.FromSeconds |> Thread.Sleep
let high = GpioPinValue.High
let low = GpioPinValue.Low
let reset (ctx:ScreenContext) =
  Array.Clear(ctx.Buffer.Value, 0, ctx.Buffer.Value.Length)
  ctx.Rst.Write low
  sleep 0.1
  ctx.Rst.Write high
  ctx

let command (c:byte) (ctx:ScreenContext) =
  ctx.Dc.Write low
  ctx.Spi.Write [|c|]
  ctx

let clamp minv maxv value =
  let m = min value maxv
  max minv m

let extendedCommand (c:byte) (ctx:ScreenContext) =
  ctx
  |> command (PCD8544_FUNCTIONSET ||| PCD8544_EXTENDEDINSTRUCTION)
  |> command c
  |> command PCD8544_FUNCTIONSET
  |> command(PCD8544_DISPLAYCONTROL ||| PCD8544_DISPLAYNORMAL)

let setContrast (contrast:byte) (ctx:ScreenContext) =
  let c = clamp 0uy 127uy contrast
  ctx |> extendedCommand (PCD8544_SETVOP ||| c)

let data (c:byte) (ctx:ScreenContext) =
  ctx.Dc.Write high
  ctx.Spi.Write [|c|]
  ctx

let setBias bias =
  extendedCommand(PCD8544_SETBIAS ||| bias)

let start (contrast, bias) (ctx:ScreenContext) =
  ctx.Dc.PinMode <- GpioPinDriveMode.Output
  ctx.Rst.PinMode <- GpioPinDriveMode.Output
  ctx
  |> reset
  |> setBias bias
  |> setContrast contrast

let display (ctx:ScreenContext) =
    ctx
    |> command PCD8544_SETYADDR
    |> command PCD8544_SETXADDR
    |> ignore
    ctx.Dc.Write high
    ctx.Spi.Write !ctx.Buffer
    ctx

let displayImage (bmp:Bitmap) (ctx:ScreenContext) =
  let bitValue (bit:byte) = 
    byte (1uy <<< int bit)
  let setPixel (x,y,color:byte) (buffer:byte array) =
    if x < LCDWIDTH && y < LCDHEIGHT
    then
      let offset = x + (y/8)* LCDWIDTH
      let v = buffer.[offset]
      let bv = bitValue(byte y % 8uy)
      let nv = if color > 0uy then v ||| bv else v &&& ~~~bv
      buffer.SetValue(nv, offset)
  for x in 0 .. bmp.Width-1 do
    for y in 0 .. bmp.Height-1 do
      let p = bmp.GetPixel(x,y)
      let v = (p.R+p.G+p.B) / 3uy
      setPixel (x,y,v) !ctx.Buffer
  display ctx

type Text = 
  { X:int
    Y:int
    Text:string
    Font:Font }
  static member From text =
    let font = new Font("Roboto", 12.f)
    { X=0; Y=0; Font=font; Text=text }

let displayText (text:Text) ctx =
  let filename = Path.Combine(Environment.CurrentDirectory, "preview.bmp")
  let bmp = new Bitmap(LCDWIDTH, LCDHEIGHT)
  try
    use g = Graphics.FromImage(bmp)
    g.SmoothingMode <- SmoothingMode.AntiAlias
    g.InterpolationMode <- InterpolationMode.HighQualityBicubic
    g.PixelOffsetMode <- PixelOffsetMode.HighQuality
    let rectf = new RectangleF(float32 text.X, float32 text.Y, float32 LCDWIDTH, float32 LCDHEIGHT)
    g.DrawString(text.Text, text.Font, Brushes.White, rectf)
    g.Flush()
    bmp.Save filename
    displayImage bmp ctx
  finally
    bmp.Dispose() //issue if when using `use bmp ...`

type Driver (dc:GpioPin, rst:GpioPin, spi:SpiChannel) =

  do
    rst.PinMode <- GpioPinDriveMode.Output
    dc.PinMode <- GpioPinDriveMode.Output
  let buffer:byte array ref = ref (Array.zeroCreate (LCDWIDTH * LCDHEIGHT))
  
  member __.Name = "Nokia 5110/3310 PCD8544-based LCD display."

  //TODO: create a class to wrap functions for C# developers
