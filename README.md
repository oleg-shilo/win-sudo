# win-sudo

This small utility is the Windows equivalent of the Linux command `sudo`.
You can use it to elevate any process from the Windows terminal/command-prompt:
``` txt
sudo choco install <product>
```
By default, it displays UAC prompt every time you execute it.
If you prefer a Linux user experience when sudo prompts only the first time it runs, you can achieve this by changing the configuration:
```
sudo -config:run=multi
```
CLI documentation: `sudo -?`

---
This project was inspired by rather excellent Windows utility `gsudo` (https://github.com/gerardog/gsudo), which I highly recommend.
`gsudo` solves the same problem as `win-sudo` and it has reach functionality that not only overlaps with `win-sudo` but also arguebly surpasses it.  
So why `win-sudo` then? 
Whell, the trigger for this work was rather accidental. When I started setting up my new Win11 environment I discovered that `gsudo` stopped handling console redirection correctly. So I decided to quickly develop my own solution. 

> While the idea of relaunching the elevated process is generic and we all done that, [gerardog](https://github.com/gerardog) is the one who decided to utilize it so elegantly. So the all credit goes to him.

So Ihave implemented my own approach which is somewhat slightly different to `gsudo`:

- Instead of relying on `runas` verb trick win-sudo launches the elevated process via an executable `sudo-host`, which triggers elevation vial the embedded application manifest (MS recommended approach).
  _Dont't get me wrong, I still often use `runas`, buf it always feels like I am breaking the rules..._
- When "Credentials Cache" is used win-sudo completely returns control to the terminal after the elevated process execution. The same way as sudo on Linux does. This is something that gsudo does very differently. I uses the elevated host process that is effectively substitutes the terminal untill the "Credentials Cache" expires.
  _It is actually an interesting gsudo idea but it feels a little unnatural._

So I have done it. After two days developing it (it wa fun) I even published the tool on WinGet: `winget install win-sudo`.

But the irony is ... gsudo is just fine on Win11. It was my own mistake that made methink that gsudo stopped working. So it is still that excellent product that I know so long.

Meaning that the trigger for this development was wrong as win-get does not solve any problem. But only doesit in a slightly different way comparing togsudo.

You can use this repo for educational purposes or even install and use win-sudo (I do 🙂) if the product difference I described above makes sense to you. You can install it from winget. 

  
