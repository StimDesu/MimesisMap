using MelonLoader;

[assembly: MelonInfo(typeof(MimesisMap.Plugin), "MimesisMap", "1.5.2", "StimDesu")]
[assembly: MelonGame("ReLUGames", "MIMESIS")]

namespace MimesisMap
{
    public sealed class Plugin : MelonMod
    {
        public override void OnInitializeMelon()
        {
            Cfg.Init();
            MapController.Init();
            LoggerInstance.Msg($"Loaded. Big map: {Cfg.BigMapKey.Value}, minimap: {Cfg.MiniKey.Value}, settings: {Cfg.SettingsKey.Value}");
        }

        public override void OnUpdate()
        {
            SettingsWindow.Update();
            try { SettingsButton.Tick(); } catch { }
            MapController.Update();
        }

        public override void OnLateUpdate()
        {
            InputBlock.Tick();
            MapController.LateUpdate();
        }

        public override void OnGUI() => SettingsWindow.OnGUI();
    }
}
