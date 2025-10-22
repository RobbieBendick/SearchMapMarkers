using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

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


        // Pan the world map to a specific X/Z coordinate
        private void PanToPosition(double targetX, double targetZ)
        {
            var mapManager = capi.ModLoader.GetModSystem<WorldMapManager>();
            var mapDlg = mapManager?.worldMapDlg;

            if (mapDlg == null || !mapDlg.IsOpened())
            {
                capi.ShowChatMessage("[SearchMapMarkersMod] worldMapDlg is null or not opened");
                return;
            }

            // Get the map element from the dialog
            GuiElementMap mapElem = mapDlg.SingleComposer.GetElement("mapElem") as GuiElementMap;
            if (mapElem != null)
            {
                // Panning to the target position (keep Y as 0 for 2D map)
                mapElem.CenterMapTo(new BlockPos((int)targetX, 0, (int)targetZ));
                capi.ShowChatMessage($"[SearchMapMarkersMod] Panned to ({targetX:F0}, {targetZ:F0})");
            }
            else
            {
                capi.ShowChatMessage("[SearchMapMarkersMod] mapElem not found");
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
                    ShowWaypointCount();

                    // Get the first waypoint
                    var waypointLayer = mapManager.MapLayers.OfType<WaypointMapLayer>().FirstOrDefault();
                    if (waypointLayer?.ownWaypoints?.Count > 0)
                    {
                        Waypoint firstWp = waypointLayer.ownWaypoints[0];
                        Vec3d pos = firstWp.Position;

                        // Pan the map to the first waypoint
                        PanToPosition(pos.X, pos.Z);

                        capi.ShowChatMessage($"[SearchMapMarkersMod] Panning to first waypoint: {firstWp.Title}");
                    }

                    // Show our GUI overlay (the search bar)
                    if (!searchDialog.IsOpened())
                    {
                        searchDialog.TryOpen();
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

        private void ShowWaypointCount()
        {
            var mapManager = capi.ModLoader.GetModSystem<WorldMapManager>();
            WaypointMapLayer waypointLayer = null;

            foreach (var layer in mapManager.MapLayers)
            {
                if (layer is WaypointMapLayer w)
                {
                    waypointLayer = w;
                    break;
                }
            }

            if (waypointLayer?.ownWaypoints?.Count > 0)
            {
                capi.ShowChatMessage($"Waypoint count: {waypointLayer.ownWaypoints.Count}");
            }
            else
            {
                capi.ShowChatMessage("No waypoints yet (retrying...)");
                capi.Event.RegisterCallback(dt => ShowWaypointCount(), 1000);
            }
        }

        public void TryPanFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var parts = text.Split(',');
            if (parts.Length == 2 &&
                double.TryParse(parts[0], out double x) &&
                double.TryParse(parts[1], out double z))
            {
                PanToPosition(x, z);
            }
            else
            {
                capi.ShowChatMessage("[SearchMapMarkersMod] Invalid format. Use x,z (e.g., 1000,2000).");
            }
        }
    }

    // Separate dialog class for the search bar
    public class GuiDialogSearchBar : GuiDialog
    {
        private SearchMapMarkersMod modSystem;

        public override string ToggleKeyCombinationCode => "searchbargui";

        public GuiDialogSearchBar(ICoreClientAPI capi) : base(capi)
        {
            // Get reference to the main mod system
            modSystem = capi.ModLoader.GetModSystem<SearchMapMarkersMod>();
            SetupDialog();
        }

        private void OnTextChanged(string newText)
        {
            capi.ShowChatMessage($"You typed: {newText}");
            modSystem?.TryPanFromText(newText);
        }

        private void SetupDialog()
        {
            // Centered on screen
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle);

            // Position the search field inside the dialog
            ElementBounds inputBounds = ElementBounds.Fixed(0, 30, 300, 30);

            // Background & layout
            ElementBounds bgBounds = ElementBounds.Fill
                .WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(inputBounds);

            SingleComposer = capi.Gui.CreateCompo("searchbardialog", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Search Map Markers", OnCloseClicked)
                .AddTextInput(inputBounds, OnTextChanged, CairoFont.WhiteSmallText(), "searchmapmarkersinput")
                .Compose();
        }

        private void OnCloseClicked()
        {
            TryClose();
        }
    }
}
