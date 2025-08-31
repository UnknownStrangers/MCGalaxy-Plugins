//reference mscorlib.dll
//reference Newtonsoft.Json.dll
//reference System.Core.dll

// retrieved from https://f.classicube.net/topic/3141-freebuildblacklist-plugin-adds-freebuildblacklist-and-reportgrief/#comment-15441

/*
 * Made by SpicyCombo
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Newtonsoft.Json;

using MCGalaxy;
using MCGalaxy.Commands;
using MCGalaxy.DB;
using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.Tasks;

namespace Omegabuild3
{
    public sealed class FreebuildBlacklistPlugin : Plugin
    {
        public override string name { get { return "FreebuildBlacklist"; } }
        public override string MCGalaxy_Version { get { return "1.9.0.7"; } }
        public override string creator { get { return "Omegabuild 2"; } }

        #region Variables
        public const string PATH = "plugins/FreebuildBlacklist/";
        public const string PATH_BLACKLISTED_PLAYERS = PATH + "blacklistedplayers.txt";
        public const string PATH_MODERATED_MAPS = PATH + "moderatedmaps.txt";

        public const string PATH_GRIEF_REPORTS = PATH + "griefreports.json";
        public const string PATH_GRIEF_REPORTS_STATS = PATH + "griefreportstats.txt";
        public const string PATH_GRIEF_REPORTS_BKLIST = PATH + "grBlacklist.txt";


        public const string KEY_FBB_PLAYERLIST = "FBB_PLAYERLIST";

        public const string TARGET_UNBLACKLISTED_MSG = "&aYou've just been removed from the blacklist!";

        public static List<SchedulerTask> notifiedBlacklistTasks = new List<SchedulerTask>();
        public static List<string> notifiedOfBlacklist = new List<string>();

        private Command cmdFBBlacklist, cmdRG;
        public static PlayerList blacklistedPlayers, moderatedMaps, grBlacklisted;
        #endregion

        #region Methods
        internal static void RemoveNotified(SchedulerTask task)
        {
            if (task.State is string plName) notifiedOfBlacklist.Remove(plName);
            notifiedBlacklistTasks.Remove(task);
            Server.MainScheduler.Cancel(task);
        }

        public static void DisplayBlacklisted(Player p, Level lvl, out bool blacklisted)
        {
            if (moderatedMaps.Contains(lvl.name) && blacklistedPlayers.Contains(p.name) && !notifiedOfBlacklist.Contains(p.name))
            {
                blacklisted = true;
                p.Message("&WYou are blacklisted from freebuild maps!");
                p.Message("&fYou may view the reason by typing &T/MyNotes&f.");
                p.Message("&fIf you wish to appeal, please join our Discord.");
                p.Message("&fYou may find the link by typing &T/Discord&f.");

                notifiedOfBlacklist.Add(p.name);
                SchedulerTask task = Server.MainScheduler.QueueRepeat(RemoveNotified, p.name, TimeSpan.FromMinutes(5));
                notifiedBlacklistTasks.Add(task);
            }
            blacklisted = false;
        }

        internal void HandleJoiningLevel(Player p, Level lvl, ref bool canJoin)
        {
            if (moderatedMaps.Contains(lvl.name) && blacklistedPlayers.Contains(p.name) && !lvl.BuildAccess.Blacklisted.Contains(p.name))
                lvl.BuildAccess.Blacklist(Player.Console, LevelPermission.Owner, lvl, p.name);
            else if (moderatedMaps.Contains(lvl.name) && !blacklistedPlayers.Contains(p.name) && lvl.BuildAccess.Blacklisted.Contains(p.name))
                lvl.BuildAccess.Whitelist(Player.Console, LevelPermission.Owner, lvl, p.name);
        }

        internal void HandleJoinedLevel(Player p, Level prevLevel, Level level, ref bool announce)
        {
            p.BlockUntilLoad(10);
            DisplayBlacklisted(p, level, out bool blacklisted);
            if (!blacklisted && moderatedMaps.Contains(p.level.name) && p.CanUse("reportgrief"))
            {
                p.Message("&6See something, say something.");
                p.Message("&SReport all griefs you see with &T/rg&S!");
            }
        }

        public static Group GetTargetRank(Player p, string name)
        {
            Player target = string.IsNullOrEmpty(name) ? null : PlayerInfo.FindExact(name);
            if (target != null)
            {
                return target.group;
            }
            else
            {
                PlayerData data = PlayerDB.Match(p, name);
                Group group = Group.GroupIn(data.Name);
                return group;
            }
        }
        #endregion

        public override void Load(bool auto)
        {
            if (!Directory.Exists(PATH)) Directory.CreateDirectory(PATH);
            blacklistedPlayers = PlayerList.Load(PATH_BLACKLISTED_PLAYERS);
            moderatedMaps = PlayerList.Load(PATH_MODERATED_MAPS);
            grBlacklisted = PlayerList.Load(PATH_GRIEF_REPORTS_BKLIST);

            cmdFBBlacklist = new CmdFreebuildBlacklist();
            cmdRG = new CmdReportGrief();
            Command.Register(cmdFBBlacklist);
            Command.Register(cmdRG);

            OnJoiningLevelEvent.Register(HandleJoiningLevel, Priority.Low);
            OnJoinedLevelEvent.Register(HandleJoinedLevel, Priority.Low);

            Server.Extras[KEY_FBB_PLAYERLIST] = blacklistedPlayers;

            GriefReport.LoadReports();
            GriefReportStatistics.ReadLines();
        }

        public override void Unload(bool auto)
        {
            Command.Unregister(cmdFBBlacklist);
            Command.Unregister(cmdRG);

            OnJoinedLevelEvent.Unregister(HandleJoinedLevel);
            OnJoiningLevelEvent.Unregister(HandleJoiningLevel);

            foreach (SchedulerTask task in notifiedBlacklistTasks)
            {
                Server.MainScheduler.Cancel(task);
            }

            Server.Extras.Remove(KEY_FBB_PLAYERLIST);

            GriefReport.SaveReports();
            GriefReportStatistics.Save();
        }
    }

    public class CmdFreebuildBlacklist : Command2
    {
        public override string name { get { return "FreebuildBlacklist"; } }
        public override string shortcut { get { return "fbb"; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }
        public override CommandPerm[] ExtraPerms
        {
            get
            {
                return new[] {
                    new CommandPerm(LevelPermission.Owner, "can add/remove moderated maps")
                };
            }
        }
        public override string type { get { return CommandTypes.Moderation; } }

        public override void Use(Player p, string message, CommandData data)
        {
            string[] args = message.SplitSpaces(3);
            string action = args[0];

            if (action.CaselessEq("list"))
            {
                if (FreebuildBlacklistPlugin.blacklistedPlayers.Count == 0)
                { p.Message("&WNo one is blacklisted from freebuild."); }
                else
                {
                    string modifier = args.Length > 1 ? args[1] : "";
                    p.Message("&SBlacklisted players list:");
                    Paginator.Output(p, FreebuildBlacklistPlugin.blacklistedPlayers.All(),
                        player => player, name + " list", "blacklisted players", modifier);
                }
            }
            else if (action.CaselessEq("add"))
            {
                if (2 > args.Length)
                {
                    p.Message("&cPlease specify the player to blacklist.");
                    return;
                }

                string matched = PlayerInfo.FindMatchesPreferOnline(p, args[1]);
                if (string.IsNullOrEmpty(matched)) return;

                if (FreebuildBlacklistPlugin.GetTargetRank(p, matched).Permission >= p.group.Permission)
                {
                    p.Message("&WYou cannot blacklist someone with the same or higher rank than you.");
                    return;
                }

                if (!FreebuildBlacklistPlugin.blacklistedPlayers.Add(matched))
                {
                    p.Message("&b" + matched + " &Sis already blacklisted.");
                    return;
                }
                else
                {
                    FreebuildBlacklistPlugin.blacklistedPlayers.Save();
                    if (args.Length > 2)
                    {
                        Find("Warn").Use(p, matched + " &cBlacklisted from freebuild maps globally for&f " + args[2]);
                    }
                    p.Message("&aSuccessfully blacklisted &b" + matched + "&a from freebuild maps.");
                }

                Player onlineTarget = PlayerInfo.FindExact(matched);
                if (onlineTarget != null && FreebuildBlacklistPlugin.moderatedMaps.Contains(onlineTarget.level.name) && !onlineTarget.level.BuildAccess.Blacklisted.Contains(matched))
                {
                    FreebuildBlacklistPlugin.DisplayBlacklisted(onlineTarget, onlineTarget.level, out _);
                    onlineTarget.level.BuildAccess.Blacklist(Player.Console, LevelPermission.Owner, onlineTarget.level, matched);
                }
            }
            else if (action.CaselessEq("del"))
            {
                if (2 > args.Length)
                {
                    p.Message("&cPlease specify the player to unblacklist.");
                    return;
                }

                string matched = PlayerInfo.FindMatchesPreferOnline(p, args[1]);
                if (string.IsNullOrEmpty(matched)) return;

                if (FreebuildBlacklistPlugin.GetTargetRank(p, matched).Permission >= p.group.Permission)
                {
                    p.Message("&WYou cannot un-blacklist someone with the same or higher rank than you.");
                    return;
                }

                if (!FreebuildBlacklistPlugin.blacklistedPlayers.Remove(matched))
                {
                    p.Message("&b" + matched + " &Sis not blacklisted.");
                    return;
                }
                else
                {
                    FreebuildBlacklistPlugin.blacklistedPlayers.Save();
                    if (args.Length > 2)
                    {
                        Find("Note").Use(p, matched + " &aUn-blacklisted from freebuild maps globally for&f " + args[2]);
                    }
                    p.Message("&aSuccessfully removed &b" + matched + "&a from the freebuild blacklist.");
                }

                Player onlineTarget = PlayerInfo.FindExact(matched);
                if (onlineTarget != null && FreebuildBlacklistPlugin.moderatedMaps.Contains(onlineTarget.level.name) && onlineTarget.level.BuildAccess.Blacklisted.Contains(matched))
                {
                    onlineTarget.Message(FreebuildBlacklistPlugin.TARGET_UNBLACKLISTED_MSG);
                    onlineTarget.level.BuildAccess.Whitelist(Player.Console, LevelPermission.Owner, onlineTarget.level, matched);
                }
            }
            else if (action.CaselessEq("maps"))
            {
                if (FreebuildBlacklistPlugin.moderatedMaps.Count == 0)
                { p.Message("&WNo maps are being moderated as freebuild maps."); }
                else
                {
                    string modifier = args.Length > 1 ? args[1] : "";
                    p.Message("&SModerated maps:");
                    Paginator.Output(p, FreebuildBlacklistPlugin.moderatedMaps.All(),
                        map => map, name + " maps", "moderated maps", modifier);
                }
            }
            else if (action.CaselessEq("addmap"))
            {
                if (!CheckExtraPerm(p, data, 1)) return;

                if (2 > args.Length)
                {
                    p.Message("&cPlease specify the map to moderate.");
                    return;
                }

                string level = Matcher.FindMaps(p, args[1]);
                if (string.IsNullOrEmpty(level)) return;

                if (!FreebuildBlacklistPlugin.moderatedMaps.Add(level))
                {
                    p.Message("&b" + level + " &Sis already being moderated.");
                    return;
                }
                else
                {
                    FreebuildBlacklistPlugin.moderatedMaps.Save();
                    p.Message("&aSuccessfully added &b" + level + "&a to moderated maps.");
                }

                Level[] loaded = LevelInfo.Loaded.Items;
                foreach (Level lvl in loaded)
                {
                    // using caselesseq just to be sure
                    if (lvl.name.CaselessEq(level))
                    {
                        foreach (Player target in lvl.players)
                        {
                            if (FreebuildBlacklistPlugin.blacklistedPlayers.Contains(target.name))
                            {
                                lvl.BuildAccess.Blacklist(Player.Console, LevelPermission.Owner, lvl, target.name);
                                FreebuildBlacklistPlugin.DisplayBlacklisted(p, lvl, out _);
                            }
                        }
                        p.Message("&aSuccessfully synced freebuild blacklist on &b" + level + "&a.");

                        break;
                    }
                }

            }
            else if (action.CaselessEq("delmap"))
            {
                if (!CheckExtraPerm(p, data, 1)) return;


                if (2 > args.Length)
                {
                    p.Message("&cPlease specify the map to unmoderate.");
                    return;
                }

                string matched = Matcher.FindMaps(p, args[1]);
                if (string.IsNullOrEmpty(matched)) return;

                if (!FreebuildBlacklistPlugin.moderatedMaps.Remove(matched))
                {
                    p.Message("&b" + matched + " &Sis not being moderated.");
                    return;
                }
                else
                {
                    FreebuildBlacklistPlugin.moderatedMaps.Save();
                    p.Message("&aSuccessfully removed &b" + matched + "&a from moderated maps.");
                }

                Level[] loaded = LevelInfo.Loaded.Items;
                foreach (Level level in loaded)
                {
                    // using caselesseq just to be sure
                    if (level.name.CaselessEq(matched))
                    {
                        foreach (Player target in level.players)
                        {
                            if (FreebuildBlacklistPlugin.blacklistedPlayers.Contains(target.name))
                            {
                                level.BuildAccess.Blacklist(Player.Console, LevelPermission.Owner, level, target.name);
                                p.Message(FreebuildBlacklistPlugin.TARGET_UNBLACKLISTED_MSG);
                            }
                        }

                        p.Message("&aSuccessfully synced freebuild blacklist on &b" + matched + "&a.");
                        break;
                    }
                }

            }
            else
            {
                Help(p);
            }
        }

        public override void Help(Player p)
        {
            p.Message("&T/FreebuildBlacklist list");
            p.Message("&HLists blacklisted players");
            p.Message("&T/FreebuildBlacklist add/del [player] <reason>");
            p.Message("&HAdds/removes a player from the blacklist.");
            p.Message("&HIf <reason> is specified, then the player will also be warned/noted.");
            p.Message("&T/FreebuildBlacklist maps");
            p.Message("&HLists moderated freebuild maps.");
            p.Message("&T/FreebuildBlacklist addmap/delmap [map]");
            p.Message("&HAdds or removes a map from the map list.");
        }
    }

    public class GriefReport
    {
        public static List<GriefReport> Reports = new List<GriefReport>();

        public GriefReport(string reporter, string map, int x, int y, int z, long reportTime, string reason = null)
        {
            Reporter = reporter;
            Map = map;
            X = x; Y = y; Z = z;
            ReportTime = reportTime;
            Reason = reason;
        }

        public static void LoadReports()
        {
            if (!File.Exists(FreebuildBlacklistPlugin.PATH_GRIEF_REPORTS)) return;
            Reports = JsonConvert.DeserializeObject<List<GriefReport>>(File.ReadAllText(FreebuildBlacklistPlugin.PATH_GRIEF_REPORTS));
        }

        public static void SaveReports()
        {
            File.WriteAllText(FreebuildBlacklistPlugin.PATH_GRIEF_REPORTS, JsonConvert.SerializeObject(Reports, Formatting.Indented));
        }

        public static int CountReports(string player)
        {
            return Reports.Where((gr) => { return gr.Reporter.CaselessEq(player); }).Count();
        }

        public static string GetReportMsg(GriefReport r)
        {
            Position pos = new Position(r.X, r.Y, r.Z);
            return string.Format("{0} on {1} (by &b{2} &S{3} ago){4}",
                pos.BlockX + ", " + pos.BlockY + ", " + pos.BlockZ,
                r.Map,
                r.Reporter,
                (DateTime.UtcNow - DateTimeOffset.FromUnixTimeSeconds(r.ReportTime)).Shorten(true, false),
                string.IsNullOrEmpty(r.Reason) ? "" : " Reason: " + r.Reason);

        }

        // --------- non static area
        public string Reporter, Map, Reason;
        public int X, Y, Z;
        public long ReportTime;
    }

    public class GriefReportStatistics
    {
        public static string DateStarted = null;
        public static int Reports = 0, Resolved = 0, ResolvedInvalid = 0;


        public static void ReadLines()
        {
            if (!File.Exists(FreebuildBlacklistPlugin.PATH_GRIEF_REPORTS_STATS))
            {
                DateStarted = DateTime.UtcNow.ToShortDateString();
                Save();
                return;
            }
            PropertiesFile.Read(FreebuildBlacklistPlugin.PATH_GRIEF_REPORTS_STATS, ProcessLine, '=');
            if (string.IsNullOrEmpty(DateStarted))
            {
                DateStarted = DateTime.UtcNow.ToShortDateString();
            }
        }

        private static void ProcessLine(string key, string value)
        {
            switch (key.ToLower())
            {
                case "started":
                    DateStarted = value;
                    break;
                case "reports":
                    Reports = int.Parse(value);
                    break;
                case "resolved":
                    Resolved = int.Parse(value);
                    break;
                case "resolvedinvalid":
                    ResolvedInvalid = int.Parse(value);
                    break;
            }
        }

        public static void Save()
        {
            using (StreamWriter sw = new StreamWriter(FreebuildBlacklistPlugin.PATH_GRIEF_REPORTS_STATS))
            {
                sw.WriteLine("Started=" + DateStarted);
                sw.WriteLine("Reports=" + Reports);
                sw.WriteLine("Resolved=" + Resolved);
                sw.WriteLine("ResolvedInvalid=" + ResolvedInvalid);
                sw.WriteLine();
            }
        }
    }

    public class CmdReportGrief : Command2
    {
        public override string name { get { return "ReportGrief"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override string shortcut { get { return "rg"; } }
        public override CommandPerm[] ExtraPerms
        {
            get
            {
                return new CommandPerm[]
                {
                    new CommandPerm(LevelPermission.Operator, "can resolve and manage reports")
                };
            }
        }

        public override void Use(Player p, string message, CommandData data)
        {
            if (message.Length == 0) { Help(p); return; }

            string[] args = message.SplitSpaces(2);

            if (args[0].CaselessEq("here"))
            {
                if (FreebuildBlacklistPlugin.grBlacklisted.Contains(p.name))
                {
                    p.Message("You've been blacklisted from making Grief Reports.");
                    return;
                }
                if (GriefReport.CountReports(p.name) >= 5)
                {
                    p.Message("You have 5 active reports. Chill down!");
                    return;
                }
                if (!FreebuildBlacklistPlugin.moderatedMaps.Contains(p.level.name))
                {
                    p.Message("You're only allowed to make grief reports on select maps that everyone can build on.");
                    return;
                }
                GriefReport gr = new GriefReport(
                    p.name,
                    p.level.name,
                    p.Pos.X, p.Pos.Y, p.Pos.Z,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    args.Length > 1 ? args[1] : null);

                GriefReport.Reports.Add(gr);
                GriefReport.SaveReports();

                GriefReportStatistics.Reports++;
                GriefReportStatistics.Save();

                ItemPerms checkPerms = CommandExtraPerms.Find(name, 1);
                string opsMsg = "λNICK &Sreported a grief in &b" + p.level.name + "&S! View it with &T/rg list";
                Chat.MessageFrom(ChatScope.Perms, p, opsMsg, checkPerms, null, true);

                p.Message("Report sent!");
            }
            else if (args[0].CaselessEq("list"))
            {
                if (GriefReport.Reports.Count == 0)
                {
                    p.Message("There are no reports to display. Nice!");
                    return;
                }

                p.Message("Reports:");
                for (int i = 0; GriefReport.Reports.Count > i; i++)
                {
                    GriefReport r = GriefReport.Reports[i];
                    p.Message("[&a{0}&S] " + GriefReport.GetReportMsg(r), i + 1);
                }
            }
            else if (args[0].CaselessEq("stats"))
            {
                p.Message("Since &b{0}&S, there have been:", GriefReportStatistics.DateStarted);
                p.Message("&b{0} &Sgrief reports made", GriefReportStatistics.Reports);
                p.Message("&b{0} &Sgrief reports resolved", GriefReportStatistics.Resolved);
                p.Message("&b{0} &Sgrief reports resolved as invalid or duplicate", GriefReportStatistics.ResolvedInvalid);
            }
            else if (args[0].CaselessEq("go"))
            {
                if (args.Length == 1) { Help(p); return; }
                int index = 0;
                if (!CommandParser.GetInt(p, args[1], "report #", ref index, 1, GriefReport.Reports.Count)) return;

                GriefReport gr = GriefReport.Reports[index - 1];

                // copied my homework from CmdTP
                if (p.level.name != gr.Map && !PlayerActions.ChangeMap(p, gr.Map)) return;

                p.BlockUntilLoad(10); // Wait for player to spawn in new map
                p.SendPosition(new Position(gr.X, gr.Y, gr.Z), p.Rot);
            }
            else if (args[0].CaselessEq("resolve"))
            // resolve [#] <true/false>
            {
                if (!HasExtraPerm(p, p.Rank, 1)) { InvalidArguments(p, message); return; }
                args = message.SplitSpaces();
                if (2 > args.Length) { Help(p); return; }

                int index = 0;
                if (!CommandParser.GetInt(p, args[1], "report #", ref index, 1, GriefReport.Reports.Count)) return;
                GriefReport gr = GriefReport.Reports[index - 1];

                bool invalid = false;
                if (args.Length > 2 && !CommandParser.GetBool(p, args[2], ref invalid)) return;


                GriefReport.Reports.RemoveAt(index - 1);
                GriefReport.SaveReports();

                if (invalid)
                {
                    GriefReportStatistics.ResolvedInvalid++;
                }
                else
                {
                    GriefReportStatistics.Resolved++;
                }
                GriefReportStatistics.Save();

                p.Message("Report #{0} is resolved! It's now removed from the list.", index);
                ItemPerms checkPerms = CommandExtraPerms.Find(name, 1);
                string opsMsg = "λNICK &S" + (invalid ? "invalidated" : "resolved") + " the following report: " + GriefReport.GetReportMsg(gr);
                Chat.MessageFrom(ChatScope.Perms, p, opsMsg, checkPerms, null, true);

            }
            else if (args[0].CaselessEq("clear"))
            {
                if (!HasExtraPerm(p, p.Rank, 1)) { InvalidArguments(p, message); return; }

                bool confirmed = args.Length > 1 && args[1].CaselessEq("confirm");
                if (!confirmed)
                {
                    p.Message("Are you sure that you want to clear all reports?");
                    p.Message("Type &T/gr clear confirm&S to continue this action.");
                    return;
                }

                GriefReport.Reports.Clear();
                File.Delete(FreebuildBlacklistPlugin.PATH_GRIEF_REPORTS);
                ItemPerms checkPerms = CommandExtraPerms.Find(name, 1);
                string opsMsg = "λNICK &Shas cleared the grief reports list!";
                Chat.MessageFrom(ChatScope.Perms, p, opsMsg, checkPerms, null, true);

            }
            else if (args[0].CaselessEq("blacklist"))
            {
                if (!HasExtraPerm(p, p.Rank, 1)) { InvalidArguments(p, message); return; }

                if (args.Length > 1)
                {
                    string matched = PlayerInfo.FindMatchesPreferOnline(p, args[1]);
                    if (string.IsNullOrEmpty(matched)) return;

                    bool added = FreebuildBlacklistPlugin.grBlacklisted.Add(matched);
                    if (!added)
                    {
                        FreebuildBlacklistPlugin.grBlacklisted.Remove(matched);
                    }
                    FreebuildBlacklistPlugin.grBlacklisted.Save();

                    p.Message("{0} {1} {2} the &T/rg&S blacklist!", added ? "&aAdded" : "&cRemoved", p.FormatNick(matched), added ? "to" : "from");

                    return;
                }

                p.Message("&T/rg&S's blacklisted players:");
                Paginator.Output(p, FreebuildBlacklistPlugin.blacklistedPlayers.All(), name => name, "reportgrief blacklist", "blacklisted players", "all");
            }
            else
            {
                InvalidArguments(p, message);
            }
        }

        public void InvalidArguments(Player p, string message)
        {
            p.Message("Invalid args \"{0}\"", message);
            Help(p);
        }

        public override void Help(Player p)
        {
            p.Message("&T/ReportGrief here <reason>");
            p.Message("&H Use the keyword \"here\" to report grief near you.");
            p.Message("&H <reason> is optional. Recommended if the grief isn't obvious.");
            p.Message("&T/ReportGrief list &H- see unresolved reports.");
            p.Message("&T/ReportGrief stats &H- see stats.");
            p.Message("&T/ReportGrief go <number> &H- go to a report");
            if (HasExtraPerm(p, p.Rank, 1))
            {
                p.Message("&T/ReportGrief resolve [number] <invalid=false>");
                p.Message("&HResolves that grief report.");
                p.Message("&T/ReportGrief clear");
                p.Message("&HClears all grief reports.");
                p.Message("&T/ReportGrief blacklist <player>");
                p.Message("&HBlacklists/unblacklists <player> from being able to make reports.");
                p.Message("&HIf no args specified, will display all blacklisted players.");
            }
        }
    }
}
