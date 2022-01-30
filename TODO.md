# build2-VS To-Do List

- Wrapper for internal invocation and results parsing of build2 commands
- Hooking up build configurations ([reference](https://docs.microsoft.com/en-us/visualstudio/extensibility/workspace-build?view=vs-2022))
- User settings file/UI
- Language features, for buildfiles, testscripts, manifests ([reference](https://docs.microsoft.com/en-us/visualstudio/extensibility/workspace-language-services?view=vs-2022))
  - Syntax highlighting ([example](https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/master/Ook_Language_Integration))
  - Auto-completion ([example](https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/master/Ook_Language_Integration/C%23/Intellisense))
  - Error annotation
 - Target listing (a la Targets View in CMake Open Folder; [#182](https://github.com/build2/build2/issues/182))
 - Tests view (see [#182](https://github.com/build2/build2/issues/182) and [VS Test Explorer](https://docs.microsoft.com/en-us/visualstudio/test/run-unit-tests-with-test-explorer?view=vs-2022))
 - Package manager integration (dependency visualization, surfacing of basic bpkg features)
