using System;

using System.CommandLine;
using System.CommandLine.NamingConventionBinder;

using Serilog;

using Grpc.Core;

using vortex.GRPC;
using Microsoft.AspNetCore.Builder;

namespace vortex.CLI
{
    internal class Program
    {
        private enum Status {
            Success,
            Failure,
            UnknownError
        }

		private static Channel channel;
        private static Vortex.VortexClient client = null;
        private static vortex.State.Current State;

        static int Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();

			string CurrentDir = AppDomain.CurrentDomain.BaseDirectory;
			string LogFolder = System.IO.Path.Combine(new System.IO.DirectoryInfo(CurrentDir).Parent.FullName, "logs");
			string ConfigPath = System.IO.Path.Combine(new System.IO.DirectoryInfo(CurrentDir).Parent.FullName, "config.json");

            // We need to load the state just for the gRPC port
            // This will also discover the OpenID providers
            State = new State.Current(ConfigPath, LogFolder);

            // ASYNC
            //   The handlers cannot be async for some reason or the program bombs out the first time
            //   a handler calls an async function

            // Hot folder
            var cmdAddHotFolder = new Command("add", "Add hotfolder");
            cmdAddHotFolder.Add(new Argument<string>("path", "The path to add as a hotfolder"));
            cmdAddHotFolder.Add(new Option<string>("--identity", "Name of the identity provider to use when authenticating for this hot folder"));
            cmdAddHotFolder.Add(new Option<string>("--account", "H-POD account login for the hot folder account to use"));
            cmdAddHotFolder.Add(new Option<string>("--settings", "Path to a JSON file containing the settings for this hot folder"));
            cmdAddHotFolder.Handler = CommandHandler.Create<string, string, string, string>(AddHotFolder);

            var cmdRemoveHotFolder = new Command("remove", "Remove hotfolder");
            cmdRemoveHotFolder.Add(new Argument<string>("path", "The path to remove from hotfolders"));
            cmdRemoveHotFolder.Handler = CommandHandler.Create<string>(RemoveHotFolder);

            var cmdGetOptions = new Command("getoptions", "Get options for hotfolder");
            cmdGetOptions.Add(new Argument<string>("path", "The hotfolder to retrieve settings for"));
            cmdGetOptions.Add(new Option<string>("--file", "The path to write the settings to"));
            cmdGetOptions.Handler = CommandHandler.Create<string,string>(GetHotFolderSettings);

            var cmdSetOptions = new Command("setoptions", "Set options for hotfolder");
            cmdSetOptions.Add(new Argument<string>("path", "The hotfolder to update the settings for"));
            cmdSetOptions.Add(new Option<string>("--file", "The path to load the settings from"));
            cmdSetOptions.Handler = CommandHandler.Create<string,string>(SetHotFolderSettings);

            var cmdSetUser = new Command("setuser", "Update the submitting user for the hotfolder");
            cmdSetUser.Add(new Argument<string>("path", "The hotfolder to update the user for"));
            cmdSetUser.Add(new Option<string>("--identity", "Name of the identity provider to use"));
            cmdSetUser.Add(new Option<string>("--account", "H-POD account login for the hot folder submitter to use"));
            cmdSetUser.Handler = CommandHandler.Create<string,string,string>(SetHotFolderUser);

            var HotFolder = new Command("hotfolder", "Hotfolder management") {
                cmdAddHotFolder,
                cmdRemoveHotFolder,
                cmdGetOptions,
                cmdSetOptions,
                cmdSetUser,
                new Command("list", "Show all hotfolder details") {
                    Handler = CommandHandler.Create(ListHotFolders)
                }
            };

            var cmdListIdentities = new Command("list", "List available identity providers");
            cmdListIdentities.Handler = CommandHandler.Create(GetIdentityProviders);

            var cmdAddIdentity = new Command("add", "Add a new identity provider");
            cmdAddIdentity.Add(new Argument<string>("name", "The name for the identity provider"));
            cmdAddIdentity.Add(new Option<string>("--url", "A URL to the identity provider configuration"));
            cmdAddIdentity.Handler = CommandHandler.Create(AddIdentityProvider);

            var cmdRemoveIdentity = new Command("remove", "Remove a configured identity provider");
            cmdRemoveIdentity.Add(new Argument<string>("name", "The name of the identity provider to remove"));
            cmdRemoveIdentity.Handler = CommandHandler.Create(RemoveIdentityProvider);

            // Identity
            var Identity = new Command("identity", "Identity management") {
                cmdListIdentities,
                cmdAddIdentity,
                cmdRemoveIdentity
            };

            // Logging
            var cmdWatchLogging = new Command("watch", "Watching logging output");
            cmdWatchLogging.Handler = CommandHandler.Create(StreamLogs);

            var cmdEnableLogging = new Command("enable", "Enable logging relay");
            cmdEnableLogging.Handler = CommandHandler.Create(EnableLogs);

            var cmdDisableLogging= new Command("disable", "Disable logging relay");
            cmdDisableLogging.Handler = CommandHandler.Create(DisableLogs);

            var cmdLogging = new Command("logging", "Logging") {
                cmdWatchLogging,
                cmdEnableLogging,
                cmdDisableLogging
            };

            // Account
            var cmdAccountConfig = new Command("config", "Get account configuration data");
            cmdAccountConfig.Add(new Option<string>("--identity", "Name of the identity provider to use when authenticating"));
            cmdAccountConfig.Add(new Option<string>("--account", "H-POD account login to acquire configuration data from"));
            cmdAccountConfig.Add(new Option<string>("--file", "File to store the account configuration, in JSON format"));
            cmdAccountConfig.Handler = CommandHandler.Create<string,string,string>(GetAccountConfig);

            var cmdAccount = new Command("account", "Account") {
                cmdAccountConfig
            };

            var cmdServerConfigGet = new Command("get", "Get server configuration");
            cmdServerConfigGet.Handler = CommandHandler.Create(GetServerConfig);

            var cmdServerConfigSet = new Command("set", "Set server configuration");
            cmdServerConfigSet.Add(new Option<string>("--instance", "ID of the H-POD instance"));
            cmdServerConfigSet.Add(new Option<string>("--api", "Base URL of the API endpoint"));
            cmdServerConfigSet.Handler = CommandHandler.Create<string,string>(SetServerConfig);

            // Server Config
            var cmdServerConfig = new Command("config", "Server Configuration") {
                cmdServerConfigGet,
                cmdServerConfigSet
            };

            var RootCommand = new Command("vortex");

            RootCommand.Add(cmdAccount);
            RootCommand.Add(HotFolder);
            RootCommand.Add(Identity);
            RootCommand.Add(cmdLogging);
            RootCommand.Add(cmdServerConfig);

            return RootCommand.Invoke(args);
        }

        private static void ServerConnectionFailed() {
            Log.Error("Failed to connect to vortex server, please check the server is available and try again");
        }

        private static void ConnectToServer() {
            Log.Verbose("Connecting to vortex server on port "+State.Local.GRPCPort.ToString());

            channel = new Channel(string.Format("127.0.0.1:{0}", State.Local.GRPCPort), ChannelCredentials.Insecure);
			client = new Vortex.VortexClient(channel);
        }

        private static void SetServerConfig(string Instance, string API) {
            try {
                ConnectToServer();

                vortex.State.ServerConfigPatch p = new State.ServerConfigPatch() { Instance = Instance, APIEndpoint = API };

                bool success = client.SetConfig(new SetConfigRequest() { Config = Newtonsoft.Json.JsonConvert.SerializeObject(p) }).Success;

                if(success)
                    Log.Information("Configuration set successfully");
                else
                    Log.Information("Failed to set configuration");
            } catch (Grpc.Core.RpcException e) {
                ServerConnectionFailed();
            }
        }

        private static void GetServerConfig() {
            try {
                ConnectToServer();

                string config = client.GetConfig(new GetConfigRequest()).Config;

                vortex.State.ServerConfigPatch p = Newtonsoft.Json.JsonConvert.DeserializeObject<vortex.State.ServerConfigPatch>(config);

                if(p != null) {
                    Console.WriteLine("Server Configuration:");
                    Console.WriteLine("  Instance: "+p.Instance);
                    Console.WriteLine("  API Endpoint: "+p.APIEndpoint);
                } else {
                    Console.WriteLine("Failed to retrieve server configuration");
                }
            } catch (Grpc.Core.RpcException e) {
                ServerConnectionFailed();
            }
        }

        // Logging
        private static void EnableLogs() {
            try {
                ConnectToServer();

                client.EnableLogs(new EnableLogsRequest());

                Log.Information("Server log relay enabled");
            } catch (Grpc.Core.RpcException e) {
                ServerConnectionFailed();
            }
        }

        private static void DisableLogs() {
            try {
                ConnectToServer();

                client.DisableLogs(new DisableLogsRequest());

                Log.Information("Server log relay disabled");
            } catch (Grpc.Core.RpcException e) {
                ServerConnectionFailed();
            }
        }

        private static void StreamLogs() {
            LogServer ls = new LogServer();
            ls.Start();

            Log.Information("Log sink started, waiting for logs...");

            while(true) {
                ls.DumpLatest();

                System.Threading.Thread.Sleep(100);
            }
        }

        // Identity Providers
        private static void GetIdentityProviders() {
            try {
                ConnectToServer();

                GetIdentityProvidersResponse r = client.GetIdentityProviders(new GetIdentityProvidersRequest());

                List<vortex.State.IdentityProvider> Providers = Newtonsoft.Json.JsonConvert.DeserializeObject<List<vortex.State.IdentityProvider>>(r.Identityproviders);

                if(Providers != null) {
                    if(Providers.Count > 0) {
                        Console.WriteLine("Available identity providers:");

                        foreach(vortex.State.IdentityProvider I in Providers) {
                            Console.WriteLine(" * {0}", I.Name);
                            Console.WriteLine("    Authority: {0}", I.Authority);
                            Console.WriteLine("    Issuer: {0}", I.IssuerRegex);
                        }
                    } else
                        Console.WriteLine("No identity providers configured");
                } else {
                    Console.WriteLine("Failed to evaluate identity providers: {0}", r.Identityproviders);
                }

            } catch (Grpc.Core.RpcException e) {
                ServerConnectionFailed();
            }
        }

        private static void AddIdentityProvider(string Name, string URL) {
            try {
                ConnectToServer();

                System.Net.Http.HttpClient wc = new System.Net.Http.HttpClient();

                string SettingsFile = "";

                try {
                    SettingsFile = wc.GetStringAsync(URL).ConfigureAwait(false).GetAwaiter().GetResult();
                } catch (System.Net.Http.HttpRequestException eURL) {
                    Console.WriteLine("Failed to download settings from "+URL+" - "+eURL.StatusCode);
                } catch (Exception e) {
                    Console.WriteLine("Unknown error downloading settings from "+URL);
                }

                if(SettingsFile != "") {
                    AddIdentityProviderResponse r = client.AddIdentityProvider(new AddIdentityProviderRequest() { Name = Name, Settings = SettingsFile });

                    if(r != null) {
                        if(r.Success) {
                            Console.WriteLine("Identity provider added successfully");
                        } else {
                            Console.WriteLine("Failed to add identity provider - check the URL and try again");
                        }
                    } else {
                        Console.WriteLine("Unknown error adding identity provider");
                    }
                }
            } catch (Grpc.Core.RpcException e) {
                ServerConnectionFailed();
            }
        }

        private static void RemoveIdentityProvider(string Name) {
            try {
                ConnectToServer();

                RemoveIdentityProviderResponse r = client.RemoveIdentityProvider(new RemoveIdentityProviderRequest() { Name = Name });

                if(r != null) {
                    if(r.Success) {
                        Console.WriteLine("Identity provider removed");
                    } else {
                        Console.WriteLine("Failed to remove identity provider");
                    }
                } else {
                    Console.WriteLine("Unknown error removing identity provider");
                }
            } catch (Grpc.Core.RpcException e) {
                ServerConnectionFailed();
            }
        }

        // Hot Folders
        private static void AddHotFolder(string Path, string Identity, string Account, string Settings = "") {
            try {
                bool FoundIDP = false;

                ConnectToServer();

                GetIdentityProvidersResponse r = client.GetIdentityProviders(new GetIdentityProvidersRequest());

                vortex.State.User U = new State.User() { IdentityProviderName = Identity, AccountLogin = Account };

                List<vortex.State.IdentityProvider> Providers = Newtonsoft.Json.JsonConvert.DeserializeObject<List<vortex.State.IdentityProvider>>(r.Identityproviders);

                if(Providers != null) {
                    foreach(vortex.State.IdentityProvider P in Providers) {
                        if(P.Name==Identity) {
                            SystemBrowser B = new SystemBrowser();

                            P.AcquireNewTokenAsync(U, B, B.Port).ConfigureAwait(false).GetAwaiter().GetResult();

                            FoundIDP = true;
                            break;
                        }
                    }
                }

                if(FoundIDP) {
                    if(U.IdentityToken != null) {
                        vortex.State.HotFolderPatch hfp = new State.HotFolderPatch();

                        if(!string.IsNullOrEmpty(Settings)) {
                            if(System.IO.File.Exists(Settings)) {
                                hfp = Newtonsoft.Json.JsonConvert.DeserializeObject<vortex.State.HotFolderPatch>(System.IO.File.ReadAllText(Settings));
                            } else {
                                Console.WriteLine("Abort: Specified settings file {0} does not exist", Settings);
                                return;
                            }
                        }

                        vortex.State.HotFolder hf = new State.HotFolder() {
                                Path = Path,
                                SubmittingUser = U,
                                ExportReady = hfp.ExportReady,
                                settings = hfp.settings,
                                ValidateAddress = hfp.ValidateAddress,
                                AbortOnValidationFailure = hfp.AbortOnValidationFailure
                            };

                        try {
                            AddHotFolderResponse hfr = client.AddHotFolder(new AddHotFolderRequest() { Hotfolder = Newtonsoft.Json.JsonConvert.SerializeObject(hf) });

                            if(hfr.Success)
                                Console.WriteLine("Hot folder {0} added", Path);
                            else {
                                Console.WriteLine("Failed to add hotfolder {0}", Path);
                                Console.WriteLine("Possible reasons:");
                                Console.WriteLine("  * Hot folder is already being monitored");
                                Console.WriteLine("  * Hot folder doesn't exist");
                                Console.WriteLine("  * Failed to authenticate the specified user");
                                Console.WriteLine("  * No settings file was provided and the account doesn't have a full set of default print options configured");
                                Console.WriteLine("  * Provided settings file was invalid");
                            }
                        } catch (Grpc.Core.RpcException e) {
                            Log.Verbose(e, "gRPC call failed");
                            ServerConnectionFailed();
                        }
                    } else {
                        Console.WriteLine("Failed to identify user, hotfolder not added");
                    }
                } else {
                    if(Providers != null) {
                        Log.Error("Identity provider not found.  Valid settings are:");
                        foreach(var P in Providers) {
                            Log.Error("  "+P.Name);
                        }
                    } else {
                        Log.Error("Identity provider not found - there are no configured identity providers");
                    }
                }
            } catch (Grpc.Core.RpcException e) {
                ServerConnectionFailed();
            }
        }

        private static void RemoveHotFolder(string Path) {
            try {
                ConnectToServer();

                RemoveHotFolderResponse r = client.RemoveHotFolder(new RemoveHotFolderRequest() { Path = Path });

                if(r.Success)
                    Console.WriteLine("Removed hot folder "+Path);
                else
                    Console.WriteLine("Failed to remove hotfolder "+Path);
            } catch (Grpc.Core.RpcException e) {
                ServerConnectionFailed();
            }
        }

        private static void ListHotFolders() {
            try {
                ConnectToServer();

                GetHotFoldersResponse r = client.GetHotFolders(new GetHotFoldersRequest());

                if(r.Hotfolders != null ) {
                    List<vortex.State.HotFolder> HotFolders = Newtonsoft.Json.JsonConvert.DeserializeObject<List<vortex.State.HotFolder>>(r.Hotfolders);

                    if(HotFolders != null) {
                        if(HotFolders.Count > 0) {
                            Console.WriteLine("Current Hot Folders:");
                            foreach(var hf in HotFolders) {
                                Console.Write("  "+hf.Snapshot);

                                if(hf.ExportReady)
                                    Console.Write(" [ExportReady]");

                                Console.WriteLine("");
                            }
                        } else {
                            Console.WriteLine("There are no hot folders currently configured");
                        }
                    } else
                        Console.WriteLine("No hotfolders returned");
                } else {
                    Console.WriteLine("Null hotfolders returned");
                }
            } catch (Grpc.Core.RpcException e) {
                ServerConnectionFailed();
            }
        }

        private static void SetHotFolderSettings(string Path, string File) {
            vortex.State.HotFolderPatch p = null;

            try {
                ConnectToServer();

                try {
                    p = Newtonsoft.Json.JsonConvert.DeserializeObject<vortex.State.HotFolderPatch>(System.IO.File.ReadAllText(File));
                } catch {
                    Console.WriteLine("Unable to parse your hotfolder configuration file, please check the format is valid JSON and try again");
                }

                if(p != null) {
                    PatchHotFolderResponse r = client.PatchHotFolder(new PatchHotFolderRequest() { Path = Path, Patch = Newtonsoft.Json.JsonConvert.SerializeObject(p) });

                    if(r.Success) {
                        Console.WriteLine("Hot folder "+Path+" updated successfully");
                    }
                }
            } catch (Grpc.Core.RpcException e) {
                ServerConnectionFailed();
            }
        }

        private static void SetHotFolderUser(string Path, string Identity, string Account) {
            vortex.State.User u = null;

            try {
                bool FoundIDP = false;

                ConnectToServer();

                GetIdentityProvidersResponse r = client.GetIdentityProviders(new GetIdentityProvidersRequest());

                u = new State.User() { IdentityProviderName = Identity, AccountLogin = Account };

                List<vortex.State.IdentityProvider> Providers = Newtonsoft.Json.JsonConvert.DeserializeObject<List<vortex.State.IdentityProvider>>(r.Identityproviders);

                if(Providers != null) {
                    foreach(vortex.State.IdentityProvider P in Providers) {
                        if(P.Name==Identity) {
                            SystemBrowser B = new SystemBrowser();

                            P.AcquireNewTokenAsync(u, B, B.Port).ConfigureAwait(false).GetAwaiter().GetResult();

                            FoundIDP=true;
                            break;
                        }
                    }
                }

                if(FoundIDP) {
                    if(!string.IsNullOrEmpty(u.IdentityToken)) {
                        SetHotFolderUserResponse r2 = client.SetHotFolderUser(new SetHotFolderUserRequest() { Path = Path, User = Newtonsoft.Json.JsonConvert.SerializeObject(u) });

                        if(r2.Success) {
                            Log.Information($"Hot folder {Path} updated successfully");
                        } else {
                            Log.Error($"Failed to update hot folder {Path} with new token ({r2.Error})");
                        }
                    } else
                        Log.Error($"Failed to get identity token for {Path}");
                } else {
                    if(Providers != null) {
                        Log.Error("Identity provider not found.  Valid settings are:");
                        foreach(var P in Providers) {
                            Log.Error("  "+P.Name);
                        }
                    } else {
                        Log.Error("Identity provider not found - there are no configured identity providers");
                    }
                }
            } catch (Grpc.Core.RpcException e) {
                ServerConnectionFailed();
            }
        }

        private static void GetHotFolderSettings(string Path, string File) {
            try {
                ConnectToServer();

                GetHotFoldersResponse r = client.GetHotFolders(new GetHotFoldersRequest());

                if(r.Hotfolders != null ) {
                    List<vortex.State.HotFolder> HotFolders = Newtonsoft.Json.JsonConvert.DeserializeObject<List<vortex.State.HotFolder>>(r.Hotfolders);
                    bool found=false;

                    if(HotFolders != null) {
                        foreach(vortex.State.HotFolder hf in HotFolders) {
                            if(hf.Path.ToLower() == Path.ToLower()) {
                                System.IO.File.WriteAllText(File, Newtonsoft.Json.JsonConvert.SerializeObject(new vortex.State.HotFolderPatch(hf)));
                                Console.WriteLine("Hot folder configuration written to "+File);
                                found=true;
                                break;
                            }
                        }
                    }

                    if(!found)
                        Console.WriteLine("Hot folder not configured");
                }
            } catch (Grpc.Core.RpcException e) {
                ServerConnectionFailed();
            }
       }

       private static void GetAccountConfig(string Identity, string Account, string File) {
            try {
                ConnectToServer();

                GetIdentityProvidersResponse r = client.GetIdentityProviders(new GetIdentityProvidersRequest());

                vortex.State.User U = new State.User() { IdentityProviderName = Identity, AccountLogin = Account };

                List<vortex.State.IdentityProvider> Providers = Newtonsoft.Json.JsonConvert.DeserializeObject<List<vortex.State.IdentityProvider>>(r.Identityproviders);

                if(Providers != null) {
                    foreach(vortex.State.IdentityProvider P in Providers) {
                        if(P.Name==Identity) {
                            SystemBrowser B = new SystemBrowser();

                            P.AcquireNewTokenAsync(U, B, B.Port).ConfigureAwait(false).GetAwaiter().GetResult();
                            break;
                        }
                    }
                }

                if(U.IdentityToken != null) { 
                    GetAccountConfigurationResponse cfg = client.GetAccountConfiguration(new GetAccountConfigurationRequest() { User = Newtonsoft.Json.JsonConvert.SerializeObject(U) });

                    System.IO.File.WriteAllText(File, cfg.Config);

                    Console.WriteLine("Configuration written to "+File);
                }
            } catch (Grpc.Core.RpcException e) {
                ServerConnectionFailed();
            }
       }
    }
}