# vortex

This is the next generation suite of client utilities for H-POD.

Requires .Net 6 on Windows or any .Net Standard 2.0 compliant implementation of the framework on other platforms.

vortex.UI and vortex.WindowsService require .Net 6 as those are Windows-specific modules.  The rest are .Net Standard 2.0 so can be used on other platforms with only the UI and service/daemon component needing rewriting for those platforms.

vortex.UI provides the user interface.

vortex.Server runs a gRPC server and handles all H-POD API interactions, including config refresh and file submission.

vortex.UI communicates with vortex.Server over a gRPC channel to initiate config refreshes and file submissions and monitor their progress, leaving vortex.UI to only do the UI work.

vortex.API provides the auto-generated proxy classes for communicating with the H-POD API.  This library is generated directly from the OpenAPI JSON document provided by H-POD API v3

vortex.State is a shared library for representing the state of a client - the current account configuration, the driver behaviour settings, the user's authentication details and tokens, amongst other things.
