using TitleRenamed.Entries;
using TitleRenamed.Strings;
using Dalamud.Plugin.Services;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Collections.Generic;
using System;

namespace TitleRenamed
{
    internal sealed class NameplateHelper : IDisposable
    {
        private readonly TitleRenameMap renameMap;
        private readonly INamePlateGui nameplateGui;
        private readonly IClientState clientState;

        public bool ignoreUpdates = false;

        internal NameplateHelper(TitleRenameMap map, INamePlateGui _nameplateGui, IClientState _clientState)
        {
            nameplateGui = _nameplateGui;
            clientState = _clientState;
            renameMap = map ?? throw new ArgumentNullException(paramName: nameof(map));

            nameplateGui.OnNamePlateUpdate += OnNameplateUpdate;
        }

        private void OnNameplateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
        {
            if (ignoreUpdates || context.AddonAddress == nint.Zero || clientState.LocalPlayer is not { } player)
            {
                return;
            }
            foreach (var handler in handlers)
            {
                if (handler.NamePlateKind is not NamePlateKind.PlayerCharacter || handler.GameObjectId != player.GameObjectId)
                {
                    continue;
                }
                if (handler.Title is null || !handler.DisplayTitle || handler.Title == SeString.Empty)
                {
                    continue;
                }
                string before = $"Before: {handler.Title.TextValue}, prefix:{handler.IsPrefixTitle}, display:{handler.DisplayTitle}";
                bool modified = ModifyNamePlateTitle(handler);

#if DEBUG
                if (modified)
                {
                    string after = $"After: {handle.TitleParts.Text.TextValue}, prefix:{handle.IsPrefixTitle}, display:{handle.DisplayTitle}";
                    Util.LogDebug($"Modifying nameplate title:\n\t{before}\n\t{after}");
                }
#endif
            }
        }

        private unsafe bool ModifyNamePlateTitle(INamePlateUpdateHandler handle)
        {
            string oldTitle = handle.Title.TextValue.Trim(ClientStringHelper.TitleLeftBracket).Trim(ClientStringHelper.TitleRightBracket);
            if (!renameMap.TryGetValue(oldTitle, out var renameEntry) || renameEntry is null || !renameEntry.RenameEnabled)
            {
                return false;
            }
            if (renameEntry.TitleString.IsDisposed)
            {
                Util.LogError($"Renaming \"{oldTitle}\" to {renameEntry.RenamedTitle} failed: TitleString disposed");
                return false;
            }
            handle.TitleParts.Text = new SeString(new TextPayload(renameEntry.RenamedTitle));
            handle.IsPrefixTitle = renameEntry.IsPrefixTitle;
            handle.DisplayTitle = handle.DisplayTitle && renameEntry.ToDisplay;
            return true;
        }

        public void Dispose()
        {
            nameplateGui.OnNamePlateUpdate -= OnNameplateUpdate;
        }
    }
}
