using System.Net;
using UnityEngine;
using System.Security.Cryptography.X509Certificates;
using System;
using System.Threading;
using EmbedIO;
using EmbedIO.Actions;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using Debug = UnityEngine.Debug;
using Swan.Logging;
using System.Threading.Tasks;

namespace FourFun.Server
{
    /// <summary>
    /// LocalWebServer sets up an HTTPS server with WebSocket and static file hosting capabilities.
    /// </summary>
    [AddComponentMenu("4FUN/LocalWebServer")]
    public class LocalWebServer : MonoBehaviour
    {
        private static LocalWebServer currentInstance = null;

        [SerializeField]
        private bool useSSL = false;

        /// <summary>
        /// Gets the singleton instance of the <see cref="LocalWebServer"/>.
        /// </summary>
        public static LocalWebServer Instance
        {
            get
            {
                if (currentInstance == null)
                {
                    currentInstance = FindFirstObjectByType<LocalWebServer>();
                }
                if (currentInstance == null)
                {
                    GameObject gameObject = new GameObject("LocalWebServer");
                    currentInstance = gameObject.AddComponent<LocalWebServer>();
                }
                return currentInstance;
            }
        }

        private Thread serverThread;
        private WebServer webServer;
        public string localAddress;
        private string wsAddress;
        private string url;
        private List<IWebModule> webSocketModules = new List<IWebModule>();
        private Dictionary<string, string> contentReplacements = new Dictionary<string, string>();

        private void Awake()
        {
            if (currentInstance == null)
            {
                currentInstance = this;
            }
        }

        private void OnDisable()
        {
            webServer.Dispose();
            serverThread.Abort();
        }

        /// <summary>
        /// Start the Web Server. (Optional define a custom port, default: 8565)
        /// </summary>
        /// <param name="port"></param>
        public void StartWebServer(int port = 8565)
        {
            // Initialize local IP address, WebSocket, and HTTPS URLs
            localAddress = GetLocalIPAddress();
            wsAddress = $"{(useSSL ? "wss://" : "ws://")}{localAddress}:{port}";
            url = $"{(useSSL ? "https://" : "http://")}{localAddress}:{port}/";

            Debug.Log($"Starting {(useSSL ? "HTTPS" : "HTTP")} web server at: {url}");

            // Add Default Replacement Values
            AddReplaceValue("{{wsAddress}}", wsAddress);

            if (useSSL)
            {
                // Create or load a self-signed certificate
                var certificate = CreateSelfSignedCert("localhost", "4fun.gg");
                serverThread = new Thread(() => StartHttpsServer(certificate));
            }
            else
            {
                serverThread = new Thread(() => StartHttpServer());
            }

            // Start the web server on a separate thread
            serverThread.Start();
        }

        /// <summary>
        /// Adds or Updates a key-value pair for content replacement.
        /// </summary>
        /// <param name="key">The placeholder key to replace.</param>
        /// <param name="value">The replacement value for the placeholder key.</param>
        public void AddReplaceValue(string key, string value)
        {
            contentReplacements[key] = value; // Use indexer to directly add or update
        }

        /// <summary>
        /// Removes a key-value pair from content replacements if the key exists.
        /// </summary>
        /// <param name="key">The key to remove from content replacements.</param>
        public void RemoveReplaceValue(string key)
        {
            if (!contentReplacements.Remove(key))
            {
                Debug.LogWarning($"Key '{key}' does not exist and cannot be removed.");
            }
        }

        /// <summary>
        /// Adds a WebSocket module to the server.
        /// </summary>
        /// <param name="webModule">The WebSocket module to add.</param>
        public void AddWebSocketModule(IWebModule webModule)
        {
            if (webModule == null)
            {
                Debug.LogError("Cannot add a null WebSocket module.");
                return;
            }
            webSocketModules.Add(webModule);
        }

        /// <summary>
        /// Removes a WebSocket module from the server.
        /// </summary>
        /// <param name="webModule">The WebSocket module to remove.</param>
        public void RemoveWebSocketModule(IWebModule webModule)
        {
            if (webModule == null)
            {
                Debug.LogError("Cannot remove a null WebSocket module.");
                return;
            }
            if (!webSocketModules.Remove(webModule))
            {
                Debug.LogWarning("The specified WebSocket module does not exist and cannot be removed.");
            }
        }

        /// <summary>
        /// Configures and starts the HTTP server with WebSocket support and static file hosting.
        /// </summary>
        private void StartHttpServer()
        {
            var htmlPath = Path.Combine(Application.streamingAssetsPath, "HTML");

            // Set up the EmbedIO server with SSL, WebSocket, and static file hosting capabilities
            webServer = new WebServer(options => options
                .WithUrlPrefix(url)
                .WithMode(HttpListenerMode.EmbedIO));

            // Add any WebSocket modules if they exist
            foreach (var webSocketModule in webSocketModules)
            {
                Debug.Log($"Added WebSocket Module: {webSocketModule.BaseRoute.TrimEnd('/')}");
                webServer.WithModule(webSocketModule);
            }

            // Intercept requests to dynamically inject content replacements and serve static files
            webServer.WithModule(new ActionModule("/", HttpVerbs.Get, async ctx =>
            {
                // Determine the requested file, defaulting to "index.html" for root requests
                var requestedPath = ctx.RequestedPath.TrimStart('/');
                requestedPath = string.IsNullOrEmpty(requestedPath) ? "index.html" : requestedPath;
                string filePath = Path.Combine(htmlPath, requestedPath);

                // Check if the requested file exists in the HTML directory
                if (File.Exists(filePath))
                {
                    // Read file content and perform any necessary content replacements
                    string fileContent = await File.ReadAllTextAsync(filePath);

                    // Apply all content replacements in the file, if any
                    foreach (var replacement in contentReplacements)
                    {
                        fileContent = fileContent.Replace(replacement.Key, replacement.Value);
                    }

                    // Determine MIME type based on file extension
                    string extension = Path.GetExtension(filePath).ToLowerInvariant();
                    ctx.Response.ContentType = extension switch
                    {
                        ".html" => "text/html",
                        ".css" => "text/css",
                        ".js" => "application/javascript",
                        ".json" => "application/json",
                        ".png" => "image/png",
                        ".jpg" or ".jpeg" => "image/jpeg",
                        ".gif" => "image/gif",
                        ".svg" => "image/svg+xml",
                        ".ico" => "image/x-icon",
                        _ => "application/octet-stream" // Default for unknown types
                    };

                    // Send the modified content as the response
                    await ctx.SendStringAsync(fileContent, ctx.Response.ContentType, Encoding.UTF8);
                }
                else
                {
                    // If the requested file does not exist, send a 404 Not Found response
                    ctx.Response.StatusCode = 404;
                }
            }));

            // Listen for state changes.
            webServer.StateChanged += (s, e) => $"WebServer New State - {e.NewState}".Info();

            webServer.RunAsync();
        }

        /// <summary>
        /// Configures and starts the HTTPS server with WebSocket support and static file hosting.
        /// </summary>
        /// <param name="certificate">The SSL certificate for secure connections.</param>
        private void StartHttpsServer(X509Certificate2 certificate)
        {
            var htmlPath = Path.Combine(Application.streamingAssetsPath, "HTML");

            // Set up the EmbedIO server with SSL, WebSocket, and static file hosting capabilities
            webServer = new WebServer(options => options
                .WithUrlPrefix(url)
                .WithMode(HttpListenerMode.EmbedIO)
                .WithCertificate(certificate));

            // Add any WebSocket modules if they exist
            foreach (var webSocketModule in webSocketModules)
            {
                Debug.Log($"Added WebSocket Module: {webSocketModule.BaseRoute}");
                webServer.WithModule(webSocketModule);
            }


            // Intercept requests to dynamically inject content replacements and serve static files
            webServer.WithModule(new ActionModule("/", HttpVerbs.Get, async ctx =>
            {
                // Determine the requested file, defaulting to "index.html" for root requests
                var requestedPath = ctx.RequestedPath.TrimStart('/');
                requestedPath = string.IsNullOrEmpty(requestedPath) ? "index.html" : requestedPath;
                string filePath = Path.Combine(htmlPath, requestedPath);

                // Check if the requested file exists in the HTML directory
                if (File.Exists(filePath))
                {
                    // Read file content and perform any necessary content replacements
                    string fileContent = await File.ReadAllTextAsync(filePath);

                    // Apply all content replacements in the file, if any
                    foreach (var replacement in contentReplacements)
                    {
                        fileContent = fileContent.Replace(replacement.Key, replacement.Value);
                    }

                    // Determine MIME type based on file extension
                    string extension = Path.GetExtension(filePath).ToLowerInvariant();
                    ctx.Response.ContentType = extension switch
                    {
                        ".html" => "text/html",
                        ".css" => "text/css",
                        ".js" => "application/javascript",
                        ".json" => "application/json",
                        ".png" => "image/png",
                        ".jpg" or ".jpeg" => "image/jpeg",
                        ".gif" => "image/gif",
                        ".svg" => "image/svg+xml",
                        ".ico" => "image/x-icon",
                        _ => "application/octet-stream" // Default for unknown types
                    };

                    // Send the modified content as the response
                    await ctx.SendStringAsync(fileContent, ctx.Response.ContentType, Encoding.UTF8);
                }
                else
                {
                    // If the requested file does not exist, send a 404 Not Found response
                    ctx.Response.StatusCode = 404;
                }
            }));

            // Listen for state changes.
            webServer.StateChanged += (s, e) => $"WebServer New State - {e.NewState}".Info();

            webServer.RunAsync();
        }

        /// <summary>
        /// Retrieves the local IP address.
        /// </summary>
        /// <returns>The local IP address as a string.</returns>
        private string GetLocalIPAddress()
        {
            string localIP = "127.0.0.1"; // Default to localhost if IP can't be found

            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    localIP = ip.ToString();
                    break;
                }
            }

            return localIP;
        }

        /// <summary>
        /// Creates or loads a self-signed certificate for HTTPS. If an existing certificate is expired, it replaces it with a new one.
        /// </summary>
        /// <param name="certName">The DNS name for the certificate.</param>
        /// <param name="password">The password for the certificate.</param>
        /// <returns>A self-signed X509Certificate2, or null if creation failed.</returns>
        public static X509Certificate2 CreateSelfSignedCert(string certName, string password)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            string certPath = Path.Combine(Application.streamingAssetsPath, $"{certName}.pfx");

            // Check if the certificate already exists
            if (File.Exists(certPath))
            {
                Debug.Log("Certificate found. Loading...");

                // Load the existing certificate
                var certificate = new X509Certificate2(certPath, password);

                // Check if the certificate is expired
                if (DateTime.UtcNow > certificate.NotAfter)
                {
                    Debug.Log("Certificate is expired. Creating a new certificate...");

                    // Delete the expired certificate file
                    File.Delete(certPath);

                    // Create a new certificate since the existing one is expired
                    return GenerateCertificate(certName, password, certPath);
                }

                Debug.Log("Certificate is valid and not expired.");
                return certificate; // Return the existing valid certificate
            }
            else
            {
                Debug.Log("Certificate not found. Creating a new certificate...");
                return GenerateCertificate(certName, password, certPath);
            }
#else
    Debug.LogError("Certificate creation is not supported on this platform.");
    return null;
#endif
        }

        /// <summary>
        /// Generates a new self-signed certificate using PowerShell and saves it to the specified path.
        /// </summary>
        /// <param name="certName">The DNS name for the certificate.</param>
        /// <param name="password">The password for the certificate.</param>
        /// <param name="certPath">The path to save the certificate.</param>
        /// <returns>A newly created X509Certificate2.</returns>
        private static X509Certificate2 GenerateCertificate(string certName, string password, string certPath)
        {
            try
            {
                // PowerShell script to create a self-signed certificate
                string script = $@"
$cert = New-SelfSignedCertificate -DnsName ""{certName}"" -CertStoreLocation ""Cert:\CurrentUser\My"" -KeyExportPolicy Exportable -NotAfter (Get-Date).AddYears(1)
$pwd = ConvertTo-SecureString -String ""{password}"" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath ""{certPath}"" -Password $pwd
";

                // Write the PowerShell script to a temporary file
                string tempScriptPath = Path.Combine(Path.GetTempPath(), "CreateCert.ps1");
                File.WriteAllText(tempScriptPath, script);

                // Start the PowerShell process
                ProcessStartInfo startInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{tempScriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process process = new Process { StartInfo = startInfo })
                {
                    process.Start();

                    // Capture PowerShell output and errors
                    string output = process.StandardOutput.ReadToEnd();
                    string errors = process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    // Delete the temporary script file
                    File.Delete(tempScriptPath);

                    if (process.ExitCode == 0)
                    {
                        Debug.Log("Certificate created successfully.");
                        return new X509Certificate2(certPath, password);
                    }
                    else
                    {
                        Debug.LogError("Error creating certificate:\n" + errors);
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Exception while creating certificate:\n" + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Parses the query parameters from a URL and returns them as a dictionary.
        /// </summary>
        /// <param name="url">The URL containing query parameters.</param>
        /// <returns>A dictionary of parameter names and values.</returns>
        public static Dictionary<string, string> ParseUrlParameters(string url)
        {
            // Initialize a dictionary to store the parameters
            var parameters = new Dictionary<string, string>();

            // Find the index of the question mark to locate the start of the query string
            int queryStartIndex = url.IndexOf('?');

            // If there's no question mark, return an empty dictionary
            if (queryStartIndex == -1) return parameters;

            // Extract the query string part of the URL
            string queryString = url.Substring(queryStartIndex + 1);

            // Split the query string by '&' to separate each parameter
            string[] queryParams = queryString.Split('&');

            // Iterate through each parameter
            foreach (string param in queryParams)
            {
                // Split the parameter into name and value by '='
                string[] kvp = param.Split('=');

                // Check if the split resulted in a name and value
                if (kvp.Length == 2)
                {
                    string key = kvp[0];
                    string value = kvp[1];

                    // Add the parameter to the dictionary
                    parameters[key] = value;
                }
            }

            return parameters;
        }
    }
}
