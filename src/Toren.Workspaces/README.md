# Workspace module

`WorkspaceClassifier` recognizes folders and standard `.sln`, `.slnx`, and `.csproj` paths. `WorkspaceTreeService` owns the Explorer model: it reads directory children only when expanded, asks the installed `dotnet` CLI for projects in a solution, and shows references declared directly in project XML. Those references are not yet an evaluated MSBuild graph; imports, conditions, and generated items need later M1 work.

`FileRecentWorkspaceStore` keeps a small IDE-only JSON history in the OS application-data directory. It never writes into a solution or project and is not needed to build one. Expected path, CLI, XML, and history failures are returned as Toren results.
