//reference System.Core.dll

using System;
using System.Collections.Generic;
using System.Linq;

using MCGalaxy;
using MCGalaxy.Commands;

namespace AMCGalaxyServer
{
    public class StaffPlugin : Plugin
    {
        public override string name { get { return "Staff"; } }

        public override void Load(bool auto)
        {
            Command.Register(new CmdStaff());
        }

        public override void Unload(bool auto)
        {
            Command.Unregister(Command.Find("staff"));
        }
    }

    public sealed class CmdStaff : Command2
    {
        public override string name { get { return "Staff"; } }
        public override string type { get { return CommandTypes.Information; } }
        public override CommandPerm[] ExtraPerms { get { return new[] { new CommandPerm(LevelPermission.Operator, "are staff members") }; } }

        public override void Use(Player p, string message, CommandData data)
        {
            ItemPerms perms = CommandExtraPerms.Find("staff", 1);
            LevelPermission minRank = perms.MinRank;

            IEnumerable<Group> ranks = Group.GroupList.Reverse<Group>();

            p.Message("[Staff Members]");
            foreach (Group rank in ranks)
            {
                int count = rank.Players.Count;
                if (rank.Permission >= minRank && count != 0) p.Message("{0}: &f{1}", rank.ColoredName + count.Plural(), rank.Players.All().Join(", "));
            }
        }

        public override void Help(Player p)
        {
            p.Message("&T/Staff &H- Prints a list of current staff members");
        }
    }
}
