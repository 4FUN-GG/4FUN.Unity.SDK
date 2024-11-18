using System;
using System.Collections.Generic;
using UnityEngine;

namespace FourFun.Helpers
{
    /// <summary>
    /// Handles executing actions on Unity's main thread, enabling thread-safe operations from other threads.
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> _executionQueue = new Queue<Action>();
        public static UnityMainThreadDispatcher Instance { get; private set; }

        private void Awake()
        {
            // Ensure only one instance persists across scenes
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Adds an action to the execution queue to be run on the main thread.
        /// </summary>
        /// <param name="action">The action to execute on the main thread.</param>
        public static void Enqueue(Action action)
        {
            if (action == null) return;

            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }

        private void Update()
        {
            // Process queued actions on the main thread in batches to minimize lock time
            if (_executionQueue.Count > 0)
            {
                List<Action> actionsToExecute = new List<Action>();
                lock (_executionQueue)
                {
                    while (_executionQueue.Count > 0)
                    {
                        actionsToExecute.Add(_executionQueue.Dequeue());
                    }
                }

                // Execute outside of lock to improve performance
                foreach (var action in actionsToExecute)
                {
                    action.Invoke();
                }
            }
        }
    }
}