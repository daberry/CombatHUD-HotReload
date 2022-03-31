using Decal.Adapter;
using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using VirindiViewService;
using VirindiViewService.Controls;
using VirindiViewService.XMLParsers;

namespace CombatHUD_HotReloadPlugin
{
    /// <summary>
    /// This is where all your plugin logic should go.  Public fields are automatically serialized and deserialized
    /// between plugin sessions in this class.  Check out the main Plugin class to see how the serialization works.
    /// </summary>
    public class PluginLogic
    {
        // public fields will be serialized and restored between plugin sessions
        public int Counter = 0;

        // ignore a specific public field
        [System.Xml.Serialization.XmlIgnoreAttribute]
        public string PluginAssemblyDirectory;

        // references to our view stuff is kept private so it is not serialized.
        private HudView view;
        private ViewProperties properties;
        private ControlGroup controls;

        private HudButton EchoButton;
        private HudTextBox EchoText;
        private HudStaticText CounterText;
        private HudButton CounterUpButton;
        private HudButton CounterDownButton;

        private Logger _logger;
        private CoreManager Core;

        #region Startup / Shutdown
        /// <summary>
        /// Called once when the plugin is loaded
        /// </summary>
        public void Startup(NetServiceHost host, CoreManager core, string pluginAssemblyDirectory, string accountName, string characterName, string serverName)
        {
            WriteLog($"Plugin.Startup");
            _logger = new Logger();

            Core = core;

            Core.WorldFilter.ChangeObject += new EventHandler<ChangeObjectEventArgs>(WorldFilter_ChangeObject);
            Core.WorldFilter.CreateObject += new EventHandler<CreateObjectEventArgs>(WorldFilter_CreateObject);
            Core.WorldFilter.ReleaseObject += new EventHandler<ReleaseObjectEventArgs>(WorldFilter_ReleaseObject);
            Core.CharacterFilter.SpellCast += new EventHandler<SpellCastEventArgs>(CharacterFilter_SpellCast);
            Core.CharacterFilter.ChangeEnchantments += new EventHandler<ChangeEnchantmentsEventArgs>(CharacterFilter_ChangeEnchantments);


            PluginAssemblyDirectory = pluginAssemblyDirectory;
            CreateView();
        }

        /// <summary>
        /// Called when the plugin is shutting down.  Unregister from any events here and do any cleanup.
        /// </summary>
        public void Shutdown()
        {
            EchoButton.Hit -= EchoButton_Hit;
            CounterUpButton.Hit -= CounterUpButton_Hit;
            CounterDownButton.Hit -= CounterDownButton_Hit;

            Core.WorldFilter.ChangeObject -= new EventHandler<ChangeObjectEventArgs>(WorldFilter_ChangeObject);
            Core.WorldFilter.CreateObject -= new EventHandler<CreateObjectEventArgs>(WorldFilter_CreateObject);
            Core.WorldFilter.ReleaseObject -= new EventHandler<ReleaseObjectEventArgs>(WorldFilter_ReleaseObject);
            Core.CharacterFilter.SpellCast -= new EventHandler<SpellCastEventArgs>(CharacterFilter_SpellCast);
            Core.CharacterFilter.ChangeEnchantments += new EventHandler<ChangeEnchantmentsEventArgs>(CharacterFilter_ChangeEnchantments);

            view.Visible = false;
            view.Dispose();
        }
        #endregion

        #region VVS Views
        /// <summary>
        /// Create our VVS view from an xml template.  We also assign references to the ui elements, as well
        /// as register event handlers.
        /// </summary>
        private void CreateView()
        {
            new Decal3XMLParser().ParseFromResource("CombatHUD_HotReloadPlugin.Views.MainView.xml", out properties, out controls);

            // main plugin view
            view = new HudView(properties, controls);

            // ui element references
            // These name indexes in view come from the viewxml from above
            EchoButton = (HudButton)view["EchoButton"];
            EchoText = (HudTextBox)view["EchoText"];
            CounterText = (HudStaticText)view["CounterText"];
            CounterUpButton = (HudButton)view["CounterUpButton"];
            CounterDownButton = (HudButton)view["CounterDownButton"];

            // ui event handlers
            EchoButton.Hit += EchoButton_Hit;
            CounterUpButton.Hit += CounterUpButton_Hit;
            CounterDownButton.Hit += CounterDownButton_Hit;

            // update ui from state
            CounterText.Text = Counter.ToString();
        }

        private void CounterUpButton_Hit(object sender, EventArgs e)
        {
            Counter++;
            CounterText.Text = Counter.ToString();
        }

        private void CounterDownButton_Hit(object sender, EventArgs e)
        {
            Counter--;
            CounterText.Text = Counter.ToString();
        }

        private void EchoButton_Hit(object sender, EventArgs e)
        {
            CoreManager.Current.Actions.AddChatText($"You hit the button! Text was: {EchoText.Text}", 5);
        }
        #endregion

        #region Logging
        public void WriteLog(string message)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(Path.Combine(PluginAssemblyDirectory, "exceptions.txt"), true))
                {
                    writer.WriteLine($"CombatHUD_HotReloadPlugin: {message}");
                    writer.Close();
                }
            }
            catch { }
        }
        #endregion

        #region Event Handling
        private void CharacterFilter_SpellCast(object sender, SpellCastEventArgs e)
        {
            _logger.LogToChat($"SpellCast {e.SpellId} {e.TargetId} {e.EventType}");
        }

        private void CharacterFilter_ChangeEnchantments(object sender, ChangeEnchantmentsEventArgs e)
        {
            _logger.LogToChat($"ChangeEnchantments {e.Enchantment} {e.Type}");
        }

        private void WorldFilter_CreateObject(object sender, CreateObjectEventArgs e)
        {
            LogEvent(sender, e.New, GetCurrentMethod());
        }

        private void WorldFilter_ReleaseObject(object sender, ReleaseObjectEventArgs e)
        {
            LogEvent(sender, e.Released, GetCurrentMethod());
        }

        private void WorldFilter_ChangeObject(object sender, ChangeObjectEventArgs e)
        {
            LogEvent(sender, e.Changed, GetCurrentMethod());
        }

        private bool IsNotMonsterObject(ObjectClass objectClass)
        {
            return objectClass != ObjectClass.Monster;
        }

        private void LogEvent(object sender, WorldObject trigger, string functionName)
        {
            if (IsNotMonsterObject(trigger.ObjectClass))
            {
                return;
            }

            var monsterObjectCollection = Core.WorldFilter.GetByObjectClass(ObjectClass.Monster);
            _logger.LogToChat($"{functionName}. {trigger.Id} {trigger.Name} {trigger.LastIdTime} Monster Count: {monsterObjectCollection.Count}");

            var activeSpellIDs = getActiveSpellIDs(trigger);
            _logger.LogToChat($"active spells: {activeSpellIDs}");
        }

        private string GetCurrentMethod()
        {
            var st = new System.Diagnostics.StackTrace();
            var sf = st.GetFrame(1);

            return sf.GetMethod().Name;
        }

        //unused
        private int[] getActiveSpellIDs(WorldObject wo)
        {
            int[] activeSpells = new int[wo.ActiveSpellCount];

            for (var i = 0; i < wo.ActiveSpellCount; i++)
            {
                activeSpells[i] = wo.ActiveSpell(i);
            }

            return activeSpells;
        }
        #endregion
    }
}

