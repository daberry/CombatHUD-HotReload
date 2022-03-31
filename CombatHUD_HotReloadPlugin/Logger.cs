using Decal.Adapter;

namespace CombatHUD_HotReloadPlugin
{
    internal class Logger
    {
        public void LogToChat(string message)
        {
            CoreManager.Current.Actions.AddChatText($"CombatHUD: {message}", 5);
        }
    }
}