using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using System.Linq;

namespace SearchMapMarkersMod
{
    public class SearchMapMarkersMod : ModSystem
    {
        private ICoreClientAPI capi;
        private bool hookedMap = false;
        private long listenerId = 0;

        private GuiDialogSearchBar searchDialog;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            capi.ShowChatMessage("[SearchMapMarkersMod] StartClientSide called.");

            // Prepare the dialog now so it’s ready when the map opens
            searchDialog = new GuiDialogSearchBar(capi);

            // Keep checking until the world map system is ready
            listenerId = capi.Event.RegisterGameTickListener(CheckForMapManager, 500);
        }

        public void PanToPosition(double targetX, double targetZ)
        {
            var mapManager = capi.ModLoader.GetModSystem<WorldMapManager>();
            var mapDlg = mapManager?.worldMapDlg;

            if (mapDlg == null || !mapDlg.IsOpened())
            {
                capi.ShowChatMessage("[SearchMapMarkersMod] worldMapDlg is null or not opened");
                return;
            }

            GuiElementMap mapElem = mapDlg.SingleComposer.GetElement("mapElem") as GuiElementMap;
            if (mapElem != null)
            {
                mapElem.CenterMapTo(new BlockPos((int)targetX, 0, (int)targetZ));
                capi.ShowChatMessage($"[SearchMapMarkersMod] Panned to ({targetX:F0}, {targetZ:F0})");
            }
            else
            {
                capi.ShowChatMessage("[SearchMapMarkersMod] mapElem not found");
            }
        }

        // Try to find a waypoint by its title (case-insensitive)
        public void TryPanToWaypointByTitle(string searchText)
        {
            var mapManager = capi.ModLoader.GetModSystem<WorldMapManager>();
            if (mapManager == null || mapManager.worldMapDlg == null) return;

            var waypointLayer = mapManager.MapLayers.OfType<WaypointMapLayer>().FirstOrDefault();
            if (waypointLayer?.ownWaypoints == null || waypointLayer.ownWaypoints.Count == 0)
            {
                capi.ShowChatMessage("[SearchMapMarkersMod] No waypoints found.");
                return;
            }

            var match = waypointLayer.ownWaypoints
                .FirstOrDefault(wp => wp.Title != null &&
                    wp.Title.Trim().ToLower() == searchText.Trim().ToLower());

            if (match != null)
            {
                PanToPosition(match.Position.X, match.Position.Z);
                capi.ShowChatMessage($"[SearchMapMarkersMod] Panning to waypoint: {match.Title}");
            }
            else
            {
                capi.ShowChatMessage($"[SearchMapMarkersMod] No waypoint found with title: \"{searchText}\"");
            }
        }

        private void CheckForMapManager(float dt)
        {
            var mapManager = capi.ModLoader.GetModSystem<WorldMapManager>();
            if (mapManager == null) return;

            if (mapManager.worldMapDlg != null && !hookedMap)
            {
                capi.ShowChatMessage("[SearchMapMarkersMod] Hooking into map OnOpened event...");

                mapManager.worldMapDlg.OnOpened += () =>
                {
                    capi.ShowChatMessage("[SearchMapMarkersMod] Map opened!");

                    // Show GUI overlay (the search bar)
                    if (!searchDialog.IsOpened())
                    {
                        searchDialog.TryOpen();
                    }

                    // Pan to first waypoint (optional)
                    var waypointLayer = mapManager.MapLayers.OfType<WaypointMapLayer>().FirstOrDefault();
                    if (waypointLayer?.ownWaypoints?.Count > 0)
                    {
                        var firstWp = waypointLayer.ownWaypoints[0];
                        PanToPosition(firstWp.Position.X, firstWp.Position.Z);
                        capi.ShowChatMessage($"[SearchMapMarkersMod] Panning to first waypoint: {firstWp.Title}");
                    }
                };

                mapManager.worldMapDlg.OnClosed += () =>
                {
                    if (searchDialog.IsOpened())
                    {
                        searchDialog.TryClose();
                    }
                };

                hookedMap = true;
                capi.ShowChatMessage("[SearchMapMarkersMod] Map hook successful!");
                capi.Event.UnregisterGameTickListener(listenerId);
            }
        }
    }

    // 🔍 Updated GUI Dialog
    public class GuiDialogSearchBar : GuiDialog
    {
        private SearchMapMarkersMod modSystem;
        private GuiElementTextInput input;

        public override string ToggleKeyCombinationCode => "searchbargui";

        public GuiDialogSearchBar(ICoreClientAPI capi) : base(capi)
        {
            modSystem = capi.ModLoader.GetModSystem<SearchMapMarkersMod>();
            SetupDialog();
        }

        private void SetupDialog()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle);

            ElementBounds inputBounds = ElementBounds.Fixed(0, 30, 300, 30);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(inputBounds);

            SingleComposer = capi.Gui.CreateCompo("searchbardialog", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Search Map Markers", OnCloseClicked)
                .AddTextInput(inputBounds, OnTextChanged, CairoFont.WhiteSmallText(), "searchmapmarkersinput")
                .Compose();

            input = SingleComposer.GetTextInput("searchmapmarkersinput");
        }

        public override void OnKeyDown(KeyEvent args)
        {
            // Check if Enter or Numpad Enter was pressed
            if (args.KeyCode == (int)GlKeys.Enter || args.KeyCode == (int)GlKeys.KeypadEnter)
            {
                string text = input?.GetText()?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    modSystem?.TryPanToWaypointByTitle(text);
                }

                args.Handled = true;
            }
            else
            {
                // Let base class handle other keys
                base.OnKeyDown(args);
            }
        }


        private void OnTextChanged(string newText)
        {
            // optional: could update live search in future
        }

        private void OnCloseClicked()
        {
            TryClose();
        }
    }

}
