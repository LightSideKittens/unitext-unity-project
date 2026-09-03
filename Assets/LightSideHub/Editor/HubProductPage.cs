using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace LightSide.Hub
{
    /// <summary>
    /// One product: what is installed, what the registry offers, and whatever the product needs from
    /// the project beyond its own package.
    /// </summary>
    internal sealed class HubProductPage
    {
        private readonly HubProduct product;
        private readonly Action reload;
        private readonly VisualElement root = new();

        private VisualElement versionsBody;
        private VisualElement status;
        private bool includePreRelease;

        public HubProductPage(HubProduct product, Action reload)
        {
            this.product = product;
            this.reload = reload;
        }

        private bool Alive => root.panel != null;

        public VisualElement Build()
        {
            root.Clear();
            root.Add(BuildSummary());

            foreach (var requirement in product.Requirements)
                if (requirement.Kind == HubRequirement.EmbeddedFork)
                    root.Add(BuildFork(requirement));

            root.Add(BuildVersions());

            status = new VisualElement();
            root.Add(status);

            if (HubRegistry.IsConfigured) FetchVersions();
            return root;
        }

        private VisualElement BuildSummary()
        {
            var installed = HubRegistry.InstalledVersion(product.PackageName);
            var card = HubVisuals.Card();

            card.Add(HubVisuals.Row(
                HubVisuals.Title(product.DisplayName),
                HubVisuals.Spacer(),
                installed != null
                    ? HubVisuals.Badge(installed, "on")
                    : HubVisuals.Badge("Not installed")));
            card.Add(HubVisuals.Subtitle(product.Summary));

            var actions = HubVisuals.Row();
            if (!string.IsNullOrEmpty(product.DocsUrl))
                actions.Add(HubVisuals.QuietButton("Documentation",
                    () => Application.OpenURL(product.DocsUrl)));
            actions.Add(HubVisuals.Spacer());
            if (installed != null)
                actions.Add(HubVisuals.Button("Remove", Remove));
            card.Add(actions);

            if (!HubRegistry.IsConfigured)
                card.Add(HubVisuals.Status(
                    "Activate your licence before installing.", HubStatus.Warning));

            return card;
        }

        private VisualElement BuildFork(HubRequirement requirement)
        {
            var card = HubVisuals.Card();
            var active = EmbeddedFork.IsActive(requirement);
            var version = EmbeddedFork.InstalledVersion(requirement);

            card.Add(HubVisuals.Row(
                HubVisuals.Title(requirement.Package),
                HubVisuals.Spacer(),
                active && version != null
                    ? HubVisuals.Badge(version, "on")
                    : HubVisuals.Badge("Unity's own")));
            card.Add(HubVisuals.Subtitle(requirement.Summary));

            var body = new VisualElement();
            card.Add(body);

            var actions = HubVisuals.Row(HubVisuals.Spacer());
            if (active)
                actions.Add(HubVisuals.Button("Revert to Unity's", () =>
                {
                    Report("Reverting — the editor will reload…", HubStatus.Info);
                    EmbeddedFork.Revert(requirement, ReportForkResult);
                }));
            card.Add(actions);

            FetchForkVersions(requirement, body, active ? version : null);
            return card;
        }

        private VisualElement BuildVersions()
        {
            var card = HubVisuals.Card();
            var toggle = HubVisuals.Button(
                includePreRelease ? "Hide pre-releases" : "Show pre-releases", () =>
                {
                    includePreRelease = !includePreRelease;
                    Build();
                });

            card.Add(HubVisuals.Row(
                HubVisuals.Title("Versions"),
                HubVisuals.Spacer(),
                toggle,
                HubVisuals.QuietButton("Refresh", FetchVersions)));

            versionsBody = new VisualElement();
            versionsBody.Add(HubVisuals.Empty(HubRegistry.IsConfigured
                ? "Loading…"
                : "Activate your licence to see the available versions."));
            card.Add(versionsBody);
            return card;
        }

        private void FetchVersions()
        {
            var request = HubRegistry.MetadataRequest(product.PackageName, includePreRelease);
            request.SendWebRequest().completed += _ =>
            {
                var failed = request.result != UnityWebRequest.Result.Success;
                var error = request.error;
                var body = failed ? null : request.downloadHandler.text;
                request.Dispose();
                if (!Alive) return;

                versionsBody.Clear();
                if (failed)
                {
                    versionsBody.Add(HubVisuals.Empty("Could not reach the registry: " + error));
                    return;
                }

                var installed = HubRegistry.InstalledVersion(product.PackageName);
                var versions = HubRegistry.ParseVersions(body);
                if (versions.Count == 0)
                {
                    versionsBody.Add(HubVisuals.Empty("This licence grants no versions of this package."));
                    return;
                }

                foreach (var version in versions)
                    versionsBody.Add(BuildVersionRow(version, installed));
            };
        }

        private VisualElement BuildVersionRow(HubVersion version, string installed)
        {
            var isInstalled = version.Version == installed;
            var name = new Label(version.Version) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("hub__version-name");

            var row = HubVisuals.Row(name);
            if (version.IsLatest) row.Add(HubVisuals.Badge("Latest", "info"));
            if (version.IsPreRelease) row.Add(HubVisuals.Badge("Pre-release", "warn"));
            row.Add(HubVisuals.Spacer());

            if (isInstalled)
                row.Add(HubVisuals.Badge("Installed", "on"));
            else
                row.Add(HubVisuals.Button(installed == null ? "Install" : "Switch",
                    () => Install(version.Version)));

            row.AddToClassList("hub__version-row");
            row.EnableInClassList("hub__version-row--installed", isInstalled);
            return row;
        }

        private void FetchForkVersions(HubRequirement requirement, VisualElement body, string installed)
        {
            if (!HubRegistry.IsConfigured) return;

            var request = HubRegistry.MetadataRequest(requirement.Package, false);
            request.SendWebRequest().completed += _ =>
            {
                var failed = request.result != UnityWebRequest.Result.Success;
                var text = failed ? null : request.downloadHandler.text;
                request.Dispose();
                if (!Alive || failed) return;

                var versions = HubRegistry.ParseVersions(text);
                body.Clear();
                foreach (var version in Newest(versions, 4))
                    body.Add(BuildForkRow(requirement, version, installed));
            };
        }

        private VisualElement BuildForkRow(HubRequirement requirement, HubVersion version, string installed)
        {
            var isInstalled = version.Version == installed;
            var name = new Label(version.Version) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("hub__version-name");

            var row = HubVisuals.Row(name);
            if (version.IsLatest) row.Add(HubVisuals.Badge("Latest", "info"));
            row.Add(HubVisuals.Spacer());

            if (isInstalled)
                row.Add(HubVisuals.Badge("In use", "on"));
            else
                row.Add(HubVisuals.Button(installed == null ? "Use fork" : "Switch", () =>
                {
                    Report($"Installing {requirement.Package} {version.Version} — the editor will reload…",
                        HubStatus.Info);
                    EmbeddedFork.Install(requirement, version.Version, ReportForkResult);
                }));

            row.AddToClassList("hub__version-row");
            row.EnableInClassList("hub__version-row--installed", isInstalled);
            return row;
        }

        private static IEnumerable<HubVersion> Newest(List<HubVersion> versions, int count)
        {
            for (var i = 0; i < versions.Count && i < count; i++) yield return versions[i];
        }

        private void Install(string version)
        {
            try
            {
                HubRegistry.Install(product.PackageName, version);
                reload();
            }
            catch (Exception e)
            {
                Report("Install failed: " + e.Message, HubStatus.Error);
            }
        }

        private void Remove()
        {
            if (!EditorUtility.DisplayDialog($"Remove {product.DisplayName}",
                    $"Remove {product.DisplayName} from this project?", "Remove", "Cancel"))
                return;

            try
            {
                HubRegistry.Remove(product.PackageName);
                reload();
            }
            catch (Exception e)
            {
                Report("Remove failed: " + e.Message, HubStatus.Error);
            }
        }

        private void ReportForkResult(string error)
        {
            if (!Alive) return;
            if (error == null) reload();
            else Report(error, HubStatus.Error);
        }

        private void Report(string message, HubStatus kind)
        {
            if (status == null) return;
            status.Clear();
            status.Add(HubVisuals.Status(message, kind));
        }
    }
}
