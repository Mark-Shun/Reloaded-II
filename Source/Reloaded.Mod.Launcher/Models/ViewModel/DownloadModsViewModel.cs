﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reloaded.Mod.Launcher.Commands.DownloadModsPage;
using Reloaded.Mod.Launcher.Models.Model.DownloadModsPage;
using Reloaded.Mod.Launcher.Utility;
using Reloaded.Mod.Loader.Update.Utilities;
using Reloaded.Mod.Loader.Update.Utilities.Nuget;
using Reloaded.WPF.MVVM;

namespace Reloaded.Mod.Launcher.Models.ViewModel
{
    public class DownloadModsViewModel : ObservableObject, IDisposable
    {
        public string SearchQuery                                         { get; set; }
        public ObservableCollection<DownloadModEntry> DownloadModEntries  { get; set; }
        public DownloadModEntry                       DownloadModEntry    { get; set; }
        public DownloadModStatus                      DownloadModStatus   { get; set; }
        public DownloadModCommand                     DownloadModCommand  { get; set; }

        private AggregateNugetRepository _nugetRepository;
        private CancellationTokenSource _tokenSource;

        /* Construction - Deconstruction */
        public DownloadModsViewModel(AggregateNugetRepository _nugetRepository)
        {
            this._nugetRepository = _nugetRepository;
            DownloadModEntries = new ObservableCollection<DownloadModEntry>();
            DownloadModCommand = new DownloadModCommand(this);
            PropertyChanged += OnSearchQueryChanged;
            #pragma warning disable 4014
            GetSearchResults(); // Fire and forget.
            #pragma warning restore 4014
        }

        /* Business Logic */
        private async Task GetSearchResults()
        {
            _tokenSource?.Cancel();
            _tokenSource = new CancellationTokenSource();

            var searchTuples = await _nugetRepository.Search(SearchQuery, false, 50, _tokenSource.Token);
            var modEntries   = new List<DownloadModEntry>();
            foreach (var tuple in searchTuples)
                modEntries.AddRange(tuple.Generic.Select(x => new DownloadModEntry(x.Identity.Id, x.Authors, x.Description, x.Identity.Version, tuple.Repository)));

            Collections.ModifyObservableCollection(DownloadModEntries, modEntries);
        }

        private void OnSearchQueryChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SearchQuery))
                #pragma warning disable 4014
                GetSearchResults(); // Fire and forget.
                #pragma warning restore 4014
        }

        public void Dispose()
        {
            _tokenSource?.Dispose();
            DownloadModCommand?.Dispose();
        }
    }
}
