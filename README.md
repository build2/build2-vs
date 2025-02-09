# build2-vs
Visual Studio extension to integrate the build2 toolchain

This project aims to add Visual Studio IDE integration for various tasks and workflows related to developing with the build2 toolchain.
The goal is to make life easier for developers used to Visual Studio, whilst also making build2 more accessible to those who may have so far been put off trying it by the lack of IDE integration.

## LSP
A work-in-progress LSP server implementation for build2 is available as a build2 package [here](https://github.com/build2/build2-lsp-server.git). Checkout and build the package, then provide the path to the produced executable in a Visual Studio Open Folder settings file named `Build2VS.json`. This settings file can go either in your workspace's root or `.vs` folder. It can also be provided as a global user setting. This is pending some extension UI work to make more managaeable, but it appears that placing the file into `[User]/AppData/Local/Microsoft/VisualStudio/[version]/OpenFolder` works.
```
{
  "lsp": {
    "serverPath": "path/to/build2-lsp-server.exe",
    "showConsole": false
  }
}
```

## Notes
- [To-Do list](TODO.md)
