using System.Collections.Generic;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class AnimBrowserWindow
    {
        private const float StudioSelectionRefreshSeconds = 0.2f;

        private float _studioSelectionNextRefreshTime;
        private readonly List<OCIChar> _cachedStudioSelectedChars = new List<OCIChar>();
        private readonly List<string> _cachedStudioSelectedCharNames = new List<string>();
        private bool _cachedStudioHasSelectedCharacters;
        private bool _cachedVisibleItemsValid;

        private void RefreshStudioSelectionCacheIfDue(bool force)
        {
            if (!force && Time.realtimeSinceStartup < _studioSelectionNextRefreshTime)
                return;

            _studioSelectionNextRefreshTime = Time.realtimeSinceStartup + StudioSelectionRefreshSeconds;

            _cachedStudioSelectedChars.Clear();
            foreach (var oci in StudioCharacterSelection.GetSelectedCharacters())
                _cachedStudioSelectedChars.Add(oci);

            _cachedStudioSelectedCharNames.Clear();
            for (int i = 0; i < _cachedStudioSelectedChars.Count; i++)
                _cachedStudioSelectedCharNames.Add(StudioCharacterSelection.GetDisplayName(_cachedStudioSelectedChars[i]));

            _cachedStudioHasSelectedCharacters = _cachedStudioSelectedChars.Count > 0;
            SyncControlsFromSelectionIfChanged();
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
    }
}
