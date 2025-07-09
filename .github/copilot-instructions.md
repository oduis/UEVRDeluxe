# Copilot Instructions for UEVR Easy Injector (UEVRDeluxe) Solution

## Project Structure
- Ignore the UEVRDeluxePackage project, it is currently unused
- UEVRDeluxe is the main WinUI3 application
- UEVRDeluxeFunc is an Azure Function backend for managing UEVR profiles in an Azure Storage. Follow Azure Functions patterns as seen in FunctionsBase.
- 
## Coding Standards
- Simplicity & Modularity: Keep code simple and modular. Use helper classes and regions to organize related logic.
- .NET & C# Best Practices: Follow .NET 8 and C# conventions, including async/await for asynchronous operations.
- Tabs: Use tabs for indentation, not spaces.
- Braces: Place opening curly brackets on the same line as the statement.
- Naming: Use clear, descriptive names. Hungarian notation is used for some variables (e.g., VM, EXEName), but prefer .NET naming conventions for types and members.
- Documentation: Add XML documentation for non-obvious classes, methods, and properties. Do not document trivial code.
- Error Handling: Use try/catch for error handling. Log or handle exceptions using existing patterns (e.g., HandleExceptionAsync).
- Logging: Use the provided ILogger for logging in backend code.
- Comments: Do not remove existing comments. Add comments only where necessary for clarity.
- Consistency: Follow the existing code style and structure, including region usage and file organization.
- Framework: Projects target .NET 8. Use compatible libraries and APIs.

## Security
- Secrets & Configs: Do not check in secrets, licenses, or developer-specific configs (e.g., local.settings.json).
- User Input: Validate and sanitize user input, especially in UEVRDeluxeFunc. Be cautious of security implications when handling input or file operations.
