using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;

namespace PokerSocial.Systems
{
    public class CloudBallerTracker : MonoBehaviour
    {
        private static Task initializationTask;

        private void Awake()
        {
            if (initializationTask == null)
            {
                initializationTask = EnsureInitializedAsync();
            }
        }

        private async Task EnsureInitializedAsync()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    var options = new InitializationOptions();
                    await UnityServices.InitializeAsync(options);
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Debug.Log($"[CloudSave] Signed in anonymously as: {AuthenticationService.Instance.PlayerId}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CloudSave] Core Initialization / Auth Failed: {ex.Message}");
            }
        }

        public async Task<int> LoadChipsAsync(int defaultChips = 1000)
        {
            if (initializationTask == null)
            {
                initializationTask = EnsureInitializedAsync();
            }

            // Await initialization completion
            await initializationTask;

            if (UnityServices.State != ServicesInitializationState.Initialized || !AuthenticationService.Instance.IsSignedIn)
            {
                Debug.LogWarning("[CloudSave] Services not initialized or signed in. Check Project Settings > Services.");
                return defaultChips;
            }

            try
            {
                var savedData = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { "chipStack" });

                if (savedData.TryGetValue("chipStack", out var item))
                {
                    int chips = item.Value.GetAs<int>();
                    Debug.Log($"[CloudSave] Loaded {chips} chips from Unity Cloud.");
                    return chips;
                }
            }
            catch (CloudSaveException e)
            {
                Debug.LogError($"[CloudSave] Failed to load chips: {e.Message}");
            }

            return defaultChips;
        }

        public async Task SaveChipsAsync(int chips)
        {
            if (initializationTask == null)
            {
                initializationTask = EnsureInitializedAsync();
            }

            await initializationTask;

            if (UnityServices.State != ServicesInitializationState.Initialized || !AuthenticationService.Instance.IsSignedIn)
            {
                Debug.LogWarning("[CloudSave] Cannot save chips: Services not initialized.");
                return;
            }

            try
            {
                var data = new Dictionary<string, object> { { "chipStack", chips } };
                await CloudSaveService.Instance.Data.Player.SaveAsync(data);
                Debug.Log($"[CloudSave] Saved {chips} chips to Unity Cloud.");
            }
            catch (CloudSaveException e)
            {
                Debug.LogError($"[CloudSave] Failed to save chips: {e.Message}");
            }
        }
    }
}