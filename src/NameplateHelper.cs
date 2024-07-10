using TitleRenamed.Entries;
using TitleRenamed.Strings;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using System;
using Dalamud.Plugin.Services;
using Dalamud.Game.Gui.NamePlate;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using System.Linq;

namespace TitleRenamed
{
    internal sealed class NameplateHelper : IDisposable
    {
        private readonly TitleRenameMap renameMap;
        private readonly INamePlateGui nameplateGui;
        private readonly IPluginLog log;

        internal NameplateHelper(TitleRenameMap map, INamePlateGui _nameplateGui, IPluginLog _log)
        {
            nameplateGui = _nameplateGui;
            log = _log;
            renameMap = map ?? throw new ArgumentNullException(paramName: nameof(map));

            nameplateGui.OnNamePlateUpdate += OnNameplateUpdate;
        }

        private void OnNameplateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
        {
            if (context.AddonAddress == nint.Zero)
            {
                return;
            }
            foreach (var handle in handlers)
            {
                if (handle.NamePlateKind is not NamePlateKind.PlayerCharacter)
                {
                    continue;
                }
                if (handle.Title is null || !handle.DisplayTitle)
                {
                    continue;
                }
                if (handle.Title == SeString.Empty)
                {
                    foreach (var payload in handle.Title.Payloads.Where(payload => payload.Type is not PayloadType.RawText))
                    {
                        log.Debug("Title is not null, but SeString is empty? - {a}", payload.ToString() ?? "null");
                    }
                }
                var before = $"Before: {handle.Title.TextValue}, prefix:{handle.IsPrefixTitle}, display:{handle.DisplayTitle}";
                var prefix = handle.IsPrefixTitle;
                var display = handle.DisplayTitle;
                var titleText = handle.TitleParts.Text ?? SeString.Empty;
                var modified = ModifyNamePlateTitle(ref prefix, ref display, ref titleText);
                handle.IsPrefixTitle = prefix;
                handle.DisplayTitle = display;
                handle.TitleParts.Text = titleText;
                var after = $"After: {handle.Title.TextValue}, prefix:{handle.IsPrefixTitle}, display:{handle.DisplayTitle}";

                if (modified)
                { 
                    Util.LogDebug($"Modifying nameplate title:\n\t{before}\n\t{after}");
                }
            }
        }

        private unsafe bool ModifyNamePlateTitle(ref bool isPrefixTitle, ref bool displayTitle, ref SeString title)
        {
            var oldTitle = title.TextValue.Trim(ClientStringHelper.TitleLeftBracket).Trim(ClientStringHelper.TitleRightBracket);
            if (!renameMap.TryGetValue(oldTitle, out var renameEntry) || renameEntry is null || !renameEntry.RenameEnabled)
            {
                return false;
            }
            if (renameEntry.TitleString.IsDisposed)
            {
                Util.LogError($"Renaming \"{oldTitle}\" to {renameEntry.RenamedTitle} failed: TitleString disposed");
                return false;
            }
            title = renameEntry.TitleString.SeString;
            isPrefixTitle = renameEntry.IsPrefixTitle;
            displayTitle = displayTitle && renameEntry.ToDisplay;
            return true;
        }

        public void Dispose()
        {
            nameplateGui.OnNamePlateUpdate -= OnNameplateUpdate;
        }
    }
}
