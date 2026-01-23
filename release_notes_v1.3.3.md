# Release v1.3.3 - Stability & UI Polish

This release focuses on resolving runtime stability issues and improving the User Experience for the new scanning features.

## 🐛 Bug Fixes
- **Runtime Crash**: Fixed a crash when toggling "Use Multi-threading" caused by an uninitialized database connection.
- **Startup Crash**: Resolved a Dependency Injection error ("Unable to resolve IHasherService") preventing the app from starting.
- **Persistence**: Fixed an issue where the "Resume" button would disappear after restarting the application. The Pause state is now correctly saved to the database.
- **UI Layout**: Fixed an issue where filter dropdowns would stretch vertically, breaking the layout.
- **UI Visibility**: Added missing data for "Sort By" and "File Type" dropdowns.
- **Theme**: Fixed "Scan" buttons becoming unreadable (white text on white background) during hover.

## 🚀 Improvements
- **Performance**: Optimized database saving for Pause actions to prevent race conditions on exit.
- **Safety**: Added global exception handling for setting toggles.
