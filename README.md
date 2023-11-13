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
