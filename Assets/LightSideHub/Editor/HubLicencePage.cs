using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LightSide.Hub
{
    /// <summary>
    /// Where a licence token is entered and checked. Activation asks the registry what the token
    /// grants, so a token that is valid but covers nothing is told apart from one that is refused.
    /// </summary>
    internal sealed class HubLicencePage
    {
        private readonly Action reload;
        private readonly VisualElement root = new();
        private TextField field;
        private Button activate;
        private VisualElement status;

        public HubLicencePage(Action reload) => this.reload = reload;

        public VisualElement Build()
        {
            root.Clear();

            var card = HubVisuals.Card();
            card.Add(HubVisuals.Title(HubRegistry.IsConfigured ? "Licence" : "Activate LightSide"));
            card.Add(HubVisuals.Subtitle(HubRegistry.IsConfigured
                ? "This project is authenticated. Paste a new token to replace the one in use."
                : "Paste the access token from your purchase email. It is written into this project's package manifest, which is what lets Unity download your packages."));

            field = HubVisuals.Field();
            field.value = "";
            field.RegisterValueChangedCallback(_ => RefreshActivateState());

            activate = HubVisuals.PrimaryButton(
                HubRegistry.IsConfigured ? "Replace token" : "Activate", Activate);
            activate.SetEnabled(false);

            card.Add(HubVisuals.Muted("Access token"));
            card.Add(HubVisuals.Row(field, activate));

            status = new VisualElement();
            card.Add(status);

            card.Add(HubVisuals.Separator());
            card.Add(HubVisuals.Row(
                HubVisuals.Muted("Tokens are issued and revoked from your LightSide account."),
                HubVisuals.Spacer(),
                HubVisuals.QuietButton("Open account",
                    () => Application.OpenURL(HubConfig.AccountUrl))));

            root.Add(card);

            if (HubRegistry.IsConfigured) root.Add(BuildChannelCard());
            return root;
        }

        /// <summary>
        /// The pre-release switch. It exists only for a project whose token travels in the registry
        /// URL: the pre-release channel is a different URL, so a project authenticating by header
        /// cannot reach it.
        /// </summary>
        private VisualElement BuildChannelCard()
        {
            var card = HubVisuals.Card();
            card.Add(HubVisuals.Title("Channel"));

            if (!HubRegistry.SupportsPreRelease)
            {
                card.Add(HubVisuals.Subtitle(
                    "This project authenticates through your user configuration, which serves released versions only."));
                return card;
            }

            var preRelease = HubRegistry.PreReleaseChannel;
            card.Add(HubVisuals.Subtitle(preRelease
                ? "Pre-release versions are offered alongside releases."
                : "Released versions only."));
            card.Add(HubVisuals.Row(
                HubVisuals.Button(preRelease ? "Use releases only" : "Include pre-releases", () =>
                {
                    HubRegistry.Configure(HubRegistry.Token, !preRelease);
                    UnityEditor.PackageManager.Client.Resolve();
                    reload();
                })));
            return card;
        }

        private void RefreshActivateState()
            => activate.SetEnabled(HubRegistry.TokenPattern.IsMatch(field.value?.Trim() ?? ""));

        private void Activate()
        {
            var token = field.value.Trim();
            activate.SetEnabled(false);
            Report("Checking the token…", HubStatus.Info);

            var request = HubRegistry.EntitlementsRequest(token);
            request.SendWebRequest().completed += _ =>
            {
                var failed = request.result != UnityEngine.Networking.UnityWebRequest.Result.Success;
                var code = request.responseCode;
                var body = failed ? null : request.downloadHandler.text;
                var error = request.error;
                request.Dispose();

                RefreshActivateState();

                if (failed)
                {
                    Report(code == 401
                        ? "That token is not valid, or it has been revoked."
                        : "Could not reach the registry: " + error, HubStatus.Error);
                    return;
                }

                var entitled = HubRegistry.ParseEntitlements(body);
                if (entitled.Count == 0)
                {
                    Report("The token is valid but grants no packages. Check your licence in your account.",
                        HubStatus.Warning);
                    return;
                }

                try
                {
                    HubRegistry.Configure(token, preRelease: false);
                    UnityEditor.PackageManager.Client.Resolve();
                }
                catch (Exception e)
                {
                    Report("Could not write the project manifest: " + e.Message, HubStatus.Error);
                    return;
                }

                reload();
            };
        }

        private void Report(string message, HubStatus kind)
        {
            status.Clear();
            status.Add(HubVisuals.Status(message, kind));
        }
    }
}
