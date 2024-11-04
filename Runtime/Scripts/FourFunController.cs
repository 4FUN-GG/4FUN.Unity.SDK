using System.Collections.Generic;
using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FourFun
{
    /// <summary>
    /// Manages communication with the game server and tracks application state.
    /// </summary>
    [AddComponentMenu("4FUN/4FunController")]
    public class FourFunController : MonoBehaviour
    {
        private static FourFunController currentInstance = null;

        /// <summary>
        /// Gets the singleton instance of the <see cref="FourFunController"/>.
        /// </summary>
        public static FourFunController Instance
        {
            get
            {
                if (currentInstance == null)
                {
                    currentInstance = FindFirstObjectByType<FourFunController>();
                }
                if (currentInstance == null)
                {
                    GameObject gameObject = new GameObject("FourFunController");
                    currentInstance = gameObject.AddComponent<FourFunController>();
                }
                return currentInstance;
            }
        }

        private static GameServerInterface gameServerInstance = null;
        private Stopwatch idleWatch;

        [SerializeField]
        private const float maxIdleTime = 160f; // Max idle time in seconds
        private float currentIdleTime = 0f;

        private bool hasSetLoaded = false;

        // Delegate declarations for various state updates
        public delegate void OnStateSendAlive();
        public static OnStateSendAlive onStateSendAlive = null;

        public delegate void OnStateInitialized();
        public static OnStateInitialized onStateInitialized = null;

        public delegate void OnStateSetUnityAsReady();
        public static OnStateSetUnityAsReady onStateSetUnityAsReady = null;

        /// <summary>
        /// Initializes the controller and starts the idle timer.
        /// </summary>
        private void Awake()
        {
            Initialize();
            idleWatch = new Stopwatch();
            idleWatch.Start();
        }

        /// <summary>
        /// Updates the controller state each frame.
        /// </summary>
        private void Update()
        {
            // Quit application if the Escape key is released
            if (Input.GetKeyUp(KeyCode.Escape))
            {
                Application.Quit();
            }

            // Check if more than 2 seconds have passed to send an "alive" message
            if (idleWatch.ElapsedMilliseconds > 2000)
            {
                idleWatch.Reset();
                idleWatch.Start();
                SendAliveToServer();
            }

            // Increment idle time based on deltaTime and time scale
            currentIdleTime += Time.deltaTime * Mathf.Clamp(Time.timeScale, 0f, 1f);

            // Quit application if idle time exceeds 120 seconds
            if (currentIdleTime >= 120f)
            {
                Debug.LogWarning($"{this} - Closing application due to idle timeout.");
                currentIdleTime = 0f;
                Application.Quit();
            }
        }

        /// <summary>
        /// Initializes the game server connection and notifies that the controller is initialized.
        /// </summary>
        private void Initialize()
        {
            gameServerInstance ??= new GameServerInterface();
            if (Application.isEditor)
            {
                Debug.LogWarning($"[{ToString()}] No communication in Unity Editor, can be ignored while developing.");
            }
            else
            {
                try
                {
                    gameServerInstance.IsLauncherVisible();
                }
                catch (UnityException exception)
                {
                    Debug.LogWarning($"[{ToString()}] - {exception.Message}");
                }
            }
            onStateInitialized?.Invoke();
        }

        /// <summary>
        /// Handles cleanup on application quit.
        /// </summary>
        private void OnApplicationQuit()
        {
            if (gameServerInstance == null)
            {
                return;
            }
            try
            {
                SetFinished();
            }
            catch (UnityException exception)
            {
                Debug.LogError($"[{ToString()}] - {exception.Message}");
            }
            gameServerInstance = null;
        }

        /// <summary>
        /// Sets the game state to finished on the server.
        /// </summary>
        private void SetFinished()
        {
            if (gameServerInstance == null)
            {
                return;
            }
            try
            {
                gameServerInstance.SetFinished();
            }
            catch (UnityException exception)
            {
                Debug.LogError($"{ToString()} - {exception.Message}");
            }
        }

        /// <summary>
        /// Sets the Unity instance as ready and invokes the corresponding state change event.
        /// </summary>
        public void SetUnityAsReady()
        {
            SetLoaded();
            onStateSetUnityAsReady?.Invoke(); 
        }

        /// <summary>
        /// Sets the loaded state in the PhoenixController and logs any errors encountered during the process.
        /// </summary>
        /// <returns>Returns true if the loaded state was successfully set; otherwise, returns false.</returns>
        private bool SetLoaded()
        {
            if (gameServerInstance == null)
                return false;

            try
            {
                gameServerInstance.SetLoaded();
                hasSetLoaded = true;
            }
            catch (UnityException exception)
            {
                hasSetLoaded = false;
                Debug.LogError($"{this} - {exception.Message}");
            }

            return hasSetLoaded;
        }

        /// <summary>
        /// Resets delegate references to prevent memory leaks.
        /// </summary>
        private void ResetDelegates()
        {
            onStateInitialized = null;
            onStateSetUnityAsReady = null;
            onStateSendAlive = null;
        }

        /// <summary>
        /// Resets the idle time to zero.
        /// </summary>
        public void ResetIdleTime()
        {
            currentIdleTime = 0f;
        }

        /// <summary>
        /// Gets the current idle time.
        /// </summary>
        /// <returns>The current idle time in seconds.</returns>
        public float GetIdleTime()
        {
            return currentIdleTime;
        }

        /// <summary>
        /// Sends an "alive" update to the server.
        /// </summary>
        /// <returns>True if the update was sent successfully; otherwise, false.</returns>
        public bool SendAliveUpdate()
        {
            return SendAliveToServer();
        }

        /// <summary>
        /// Sends a keep-alive message to the server.
        /// </summary>
        /// <returns>True if the message was sent successfully; otherwise, false.</returns>
        private bool SendAliveToServer()
        {
            if (gameServerInstance == null)
                return false;

            try
            {
                gameServerInstance.SendAlive();
                onStateSendAlive?.Invoke();
                return true;
            }
            catch (UnityException exception)
            {
                Debug.LogError($"{this} - {exception.Message}");
                onStateSendAlive?.Invoke();
                return false;
            }
        }

        /// <summary>
        /// Loads the player positions from the game server (Phoenix).
        /// </summary>
        /// <returns>A list of booleans representing player positions; returns null if the server instance is unavailable.</returns>
        public List<bool> LoadPlayerPositions()
        {
            if (gameServerInstance == null)
            {
                return null;
            }

            List<bool> playerPositions = new List<bool>();

            try
            {
                SendAliveUpdate();
                playerPositions = gameServerInstance.GetPlayerPlaces();
            }
            catch (UnityException ex)
            {
                Debug.LogError($"{this} - Error loading player positions: {ex}");
            }

            return playerPositions;
        }

    }
}
