﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.UI.Core;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class OobeWhatsNew : Page
    {
        // Contains information for a release. Used to deserialize release JSON info from GitHub.
        private class PowerToysReleaseInfo
        {
            [JsonPropertyName("published_at")]
            public DateTimeOffset PublishedDate { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("tag_name")]
            public string TagName { get; set; }

            [JsonPropertyName("body")]
            public string ReleaseNotes { get; set; }
        }

        public OobePowerToysModule ViewModel { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OobeWhatsNew"/> class.
        /// </summary>
        public OobeWhatsNew()
        {
            this.InitializeComponent();
            ViewModel = new OobePowerToysModule(OobeShellPage.OobeShellHandler.Modules[(int)PowerToysModules.WhatsNew]);
            DataContext = ViewModel;
        }

        private static async Task<string> GetReleaseNotesMarkdown()
        {
            string releaseNotesJSON = string.Empty;
            using (HttpClient getReleaseInfoClient = new HttpClient())
            {
                // GitHub APIs require sending an user agent
                // https://docs.github.com/en/rest/overview/resources-in-the-rest-api#user-agent-required
                getReleaseInfoClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "PowerToys");
                releaseNotesJSON = await getReleaseInfoClient.GetStringAsync("https://api.github.com/repos/microsoft/PowerToys/releases");
            }

            IList<PowerToysReleaseInfo> releases = JsonSerializer.Deserialize<IList<PowerToysReleaseInfo>>(releaseNotesJSON);

            // Get the latest releases
            var latestReleases = releases.OrderByDescending(release => release.PublishedDate).Take(5);

            StringBuilder releaseNotesHtmlBuilder = new StringBuilder(string.Empty);

            // Regex to remove installer hash sections from the release notes.
            Regex removeHashRegex = new Regex(@"(\r\n)+#+ installer( SHA256)? hash(\r\n)+[0-9A-F]{64}", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            foreach (var release in latestReleases)
            {
                releaseNotesHtmlBuilder.AppendLine("# " + release.Name);
                releaseNotesHtmlBuilder.AppendLine(removeHashRegex.Replace(release.ReleaseNotes, string.Empty));
                releaseNotesHtmlBuilder.AppendLine("&nbsp;");
            }

            return releaseNotesHtmlBuilder.ToString();
        }

        private async void Page_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                string releaseNotesMarkdown = await GetReleaseNotesMarkdown();

                ReleaseNotesMarkdown.Text = releaseNotesMarkdown;
                ReleaseNotesMarkdown.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                LoadingProgressRing.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Logger.LogError("Exception when loading the release notes", ex);

                LoadingProgressRing.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                ErrorInfoBar.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
        }

        /// <inheritdoc/>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.LogOpeningModuleEvent();
        }

        /// <inheritdoc/>
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.LogClosingModuleEvent();
        }

        private void ReleaseNotesMarkdown_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            if (Uri.TryCreate(e.Link, UriKind.Absolute, out Uri link))
            {
                Process.Start(new ProcessStartInfo(link.ToString()) { UseShellExecute = true });
            }
        }
    }
}
