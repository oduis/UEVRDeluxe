# Copilot Instructions for UEVR Easy Injector (UEVRDeluxe) Solution
Be brief.

## Project Structure
- Ignore the UEVRDeluxePackage project, it is currently unused
- UEVRDeluxe is the main WinUI3 application
- UEVRDeluxeFunc is an Azure Function backend for managing UEVR profiles in an Azure Storage. Follow Azure Functions patterns as seen in FunctionsBase.

## Coding Standards
** IMPORTANT: Try to preserve the original code and the logic of the original code as much as possible **
- Tabs: Use tabs for indentation, not spaces.
- Braces: Place opening curly brackets on the same line as the statement.
- Documentation: Add XML documentation for non-obvious classes, methods, and properties. Do not document trivial code.
- Error Handling: Use try/catch for error handling. Log or handle exceptions using existing patterns (e.g., HandleExceptionAsync).
- Logging: Use the provided ILogger for logging in backend code.
- Comments: Do not remove existing comments. Add comments only where necessary for clarity.
- Consistency: Follow the existing code style and structure, including region usage and file organization.
- Tools: Prefer Copilot tools like "file_search", "write_file", "find_symbol", "code_search", "run_build" and others instead of Powershell or terminal

