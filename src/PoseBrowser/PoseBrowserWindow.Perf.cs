using System;
using System.Collections.Generic;
using System.Linq;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class PoseBrowserWindow
    {
        private const float StudioSelectionRefreshSeconds = 0.2f;

        private float _studioSelectionNextRefreshTime;
        private readonly List<OCIChar> _cachedStudioSelectedChars = new List<OCIChar>();
        private readonly List<string> _cachedStudioSelectedCharNames = new List<string>();
        private bool _cachedStudioHasSelectedCharacters;

        private List<PoseBrowserDisplayEntry>? _cachedVisibleDisplayEntries;
        private int _cachedVisiblePage = -1;
        private int _cachedVisibleItemsPerPage = -1;
        private int _cachedVisibleDisplayCount = -1;

        private List<PoseBrowserGridRow>? _cachedGridRows;
        private int _cachedGridRowsPage = -1;
        private int _cachedGridRowsColumns = -1;
        private int _cachedGridRowsDisplayCount = -1;
        private bool _cachedGridRowsImportPreview;

        private void InvalidatePoseBrowserViewCaches()
        {
            _cachedVisibleDisplayEntries = null;
            _cachedVisiblePage = -1;
            _cachedGridRows = null;
            _cachedGridRowsPage = -1;
            _gridUniformLayoutRowCount = -1;
            _thumbnailLoadNeeded = true;
            _groupMembersById = null;
            _compactBlocks = null;
        }

        private void RefreshStudioSelectionCacheIfDue(bool force)
        {
            if (!force && Time.realtimeSinceStartup < _studioSelectionNextRefreshTime)
                return;

            _studioSelectionNextRefreshTime = Time.realtimeSinceStartup + StudioSelectionRefreshSeconds;

            _cachedStudioSelectedChars.Clear();
            foreach (var oci in _dataService.GetSelectedCharacters())
                _cachedStudioSelectedChars.Add(oci);

            _cachedStudioSelectedCharNames.Clear();
            foreach (var oci in _cachedStudioSelectedChars)
                _cachedStudioSelectedCharNames.Add(PoseDataService.GetOCICharDisplayName(oci));

            _cachedStudioHasSelectedCharacters = _cachedStudioSelectedChars.Count > 0;
        }

        private IList<string> GetCachedStudioCharacterDisplayNames()
        {
            RefreshStudioSelectionCacheIfDue(force: false);
            return _cachedStudioSelectedCharNames;
        }

        private bool GetCachedStudioHasSelectedCharacters()
        {
            RefreshStudioSelectionCacheIfDue(force: false);
            return _cachedStudioHasSelectedCharacters;
        }

        private IList<OCIChar> GetCachedStudioSelectedCharacters()
        {
            RefreshStudioSelectionCacheIfDue(force: false);
            return _cachedStudioSelectedChars;
        }

        private List<PoseBrowserGridRow> GetOrBuildGridRows(
            IList<PoseBrowserDisplayEntry> visibleEntries,
            int columns)
        {
            int displayCount = _displayEntries.Count;
            bool import = ImportPreviewActive;
            if (_cachedGridRows != null &&
                _cachedGridRowsPage == _currentPage &&
                _cachedGridRowsColumns == columns &&
                _cachedGridRowsDisplayCount == displayCount &&
                _cachedGridRowsImportPreview == import)
            {
                return _cachedGridRows;
            }

            _cachedGridRows = PoseBrowserGridLayout.BuildGridRows(
                visibleEntries,
                _groupDb,
                columns,
                import ? _importPreviewGroupsById : null);
            _cachedGridRowsPage = _currentPage;
            _cachedGridRowsColumns = columns;
            _cachedGridRowsDisplayCount = displayCount;
            _cachedGridRowsImportPreview = import;
            return _cachedGridRows;
        }
    }
}
