open System
open System.Drawing
open Unosquare.RaspberryIO
open Unosquare.RaspberryIO.Gpio
open Unosquare.RaspberryIO.Native
open NokiaLcd

let nextStep text =
    printfn text
    Console.ReadKey true |> ignore

[<EntryPoint>]
let main argv =
    WiringPi.WiringPiSetup() |> ignore
    WiringPi.WiringPiSetupGpio() |> ignore

    printfn "Playing with GPIO !!!!!"

    Pi.Gpio.Pin17.PinMode <- GpioPinDriveMode.Output
    let high = GpioPinValue.High
    let low = GpioPinValue.Low

    //light on
    Pi.Gpio.Pin17.Write high

    let screen = ScreenContext.Default
    let text = 
        { Text.From "FSharp works on Raspberry Pi !" 
            with
                Font = new Font("Roboto", 10.f)
        }

    let logo = Bitmap.FromFile "logo2.bmp" :?> Bitmap

    screen
    |> start (60uy, 4uy)
    |> displayImage logo
    |> ignore

    printfn "look at your screen :)"
    nextStep "Press any key to continue ..."

    screen
    |> displayText text
    |> ignore

    printfn "enter a small text to display"
    let line = Console.ReadLine()

    screen
    |> displayText 
        { text with Text=line.TrimEnd() }
    |> ignore

    nextStep "Press any key to quit ..."

    //light off
    Pi.Gpio.Pin17.Write low
    //clear screen
    screen |> reset |> ignore
    
    0 // return an integer exit code
