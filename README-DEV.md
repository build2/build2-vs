Root of relevant API documentation: https://learn.microsoft.com/en-us/visualstudio/extensibility/open-folder?view=vs-2022

## Organization
- /Toolchain contains various classes to represent elements of build2 project/configuration state, and helpers for invoking the toolchain and consuming/parsing command output.
- /Contexts is where most of the API integration hooks are implemented. Scanners and action providers for various file types, etc.

## Approach
The extension assumes a folder containing `bdep`-initialized project(s). 

The Open Folder APIs are built around indexing files in the workspace, from which information about the project and configurations is expected to be inferred (and then cached in an index). The framework provides some built-in support for re-triggering file scanners and providers when the corresponding files have changed. One problem here for `build2` is that configuration state is held in a database, so it doesn't map very cleanly to this design. I guess it would be possible to set some kind of file watch on the database itself in order to keep the state of the integration updated as `bdep` commands are externally invoked, but this is not something that has been attempted at this point.

The initial implementation uses `buildfile`s in the workspace as a kind of proxy for a target, in that you can build/clean using a context menu on a given `buildfile` in the workspace tree view.

## Issues
The main stumbling block so far is understanding how the configuration support is intended to be used, and if it's even complete. The IDE's behaviour in terms of when it chooses to show the dropdown for switching configuration is rather confusing, and seems to be fundamentally tied in some way to what the currently selected 'Startup Project' is, which makes no sense to me since configuration switching is needed for non-executable targets too. Furthermore, it seems to store current configuration on a per-file context level, meaning there is no concept of project-wide 'active configuration' like there is for MSBuild, and instead attempting to build/clean a target will do it for whatever configuration was last selected for that particular target. 

For a non-trivial project, initial indexing can be rather slow. This is probably in part due to the above mentioned mismatch between the framework design and `build2`, but also something that could definitely be improved through better implementation in the extension and/or some tweaks to `build2`. Essentially we currently just invoke the toolchain a potentially large number of times when gathering data for indexing, and I think the slowness largely comes down to the time to constantly spin up the toolchain processes.

Related to the above, is the issue that `build2` commands on a project can't be executed in parallel, so if the extension is invoking the toolchain for something at the same time that a user does so from their terminal, there is potential for problems.

## References
MS sample for Open Folder API: https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/83759e1796f6cb9bfc509b58d7c1c0099ad5210d/Open_Folder_Extensibility

Other projects using the API:
- https://github.com/microsoft/nodejstools
- https://github.com/kitamstudios/rust-analyzer.vs
