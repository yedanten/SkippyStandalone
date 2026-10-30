using Skippy.App.Services;
using Skippy.Core.Configuration;

namespace Skippy.App.ViewModels;

/// <summary>
/// 把原插件各 UI 页面（MSQ Roulette / Gold Saucer / ... ）的文案与联动逻辑转换成可绑定的页面模型。
/// 文本保持与原插件一致，方便用户对照。
/// </summary>
public static class SettingsPageBuilder
{
    public static SettingsPageModel Build(Category category, AppHost host, bool autoPartyActive)
    {
        var config = host.Config;

        return category switch
        {
            Category.MSQRoulette => BuildMsq(host, config, autoPartyActive),
            Category.MassivePC => BuildMassivePc(host, config),
            Category.GoldSaucer => BuildGoldSaucer(host, config),
            Category.CustomTalk => BuildCustomTalk(host, config),
            Category.NormalCutscenes => BuildNormalCutscenes(host, config),
            Category.FeedBuddy => BuildFeedBuddy(host, config),
            Category.RiskySkips => BuildRiskySkips(host, config),
            Category.AutoParty => BuildAutoParty(host, config),
            Category.Behaviour => BuildBehaviour(host, config),
            Category.HookResearch => BuildHookResearch(host, config),
            _ => new SettingsPageModel { Title = category.ToString() },
        };
    }

    private static ToggleItem Toggle(string label, string description, bool value, Action<bool> set, AppHost host) =>
        ToggleItem.Create(label, description, value, changed =>
        {
            set(changed);
            host.NotifyConfigChanged();
        });

    private static SettingsPageModel BuildMsq(AppHost host, SkippyConfig config, bool autoParty)
    {
        var skipAll = config.SkipMSQRoulette;
        var sections = new List<SettingsSection>();

        var master = Toggle(
            "Skip all of the MSQ Instance Cutscenes",
            "Skips all cutscenes in Castrum Meridianum, The Praetorium, and Porta Decumana.",
            skipAll,
            value =>
            {
                config.SkipMSQRoulette = value;
                config.ExemptCastrum = false;
                config.ExemptPrae = false;
                config.ExemptPorta = false;
            },
            host);

        master.IsEnabled = !autoParty;

        sections.Add(new SettingsSection
        {
            Items = [master],
        });

        var exemptions = new List<ToggleItem>();

        foreach (var (id, label, tip, apply) in new (string Id, string Label, string Tip, Action<bool> Setter)[]
                 {
                     ("Castrum", "Castrum Meridianum", "Cutscenes will play normally inside Castrum Meridianum.",
                         v => config.ExemptCastrum = v),
                     ("Prae", "The Praetorium", "Cutscenes will play normally inside The Praetorium.",
                         v => config.ExemptPrae = v),
                     ("Porta", "Porta Decumana", "Cutscenes will play normally inside Porta Decumana.",
                         v => config.ExemptPorta = v),
                 })
        {
            var value = id switch
            {
                "Castrum" => config.ExemptCastrum,
                "Prae" => config.ExemptPrae,
                _ => config.ExemptPorta,
            };

            var item = Toggle(
                skipAll ? $"Exempt: {label}" : $"Skip: {label}",
                skipAll ? tip : tip.Replace("play normally", "be skipped"),
                value,
                setterValue => apply(setterValue),
                host);

            item.IsEnabled = !autoParty;
            exemptions.Add(item);
        }

        sections.Add(new SettingsSection
        {
            Header = skipAll ? "Exemptions" : "Skip Specific",
            Description = skipAll
                ? "Any enabled entries in this category will be skipped individually in case you want to only skip specific instances, rather than all of them by using the toggle in the above section."
                : "Any ticked entries in this category will be exempt from the global skip above and cutscenes will play as normal in those instances.",
            Items = exemptions,
        });

        return new SettingsPageModel
        {
            Title = "MSQ Roulette",
            Intro = "This section covers cutscenes that are triggered within the Main Scenario Roulette dungeons & trials. "
                    + "Whether they are played via the roulette or by queuing directly into the instance, the cutscenes will still be skipped if this section is enabled.\n"
                    + "If any party members do not have Skippy installed, or they don't have this section toggled, you will have to wait in the loading area until they finish viewing the cutscene(s).\n\n"
                    + "Side Note: You can enable this option mid-run if you forgot beforehand and it will start skipping cutscenes after you enable it "
                    + "(though it will not skip a cutscene you're currently viewing).",
            Notice = autoParty
                ? "Auto-Party Mode is currently active - this section is managed automatically and cannot be edited manually.\n\n"
                  + "Disable Auto-Party Mode in the Auto-Party Mode category to regain control and be able to pick options here."
                : null,
            NoticeColor = "#A6A666",
            Sections = sections,
        };
    }

    private static SettingsPageModel BuildMassivePc(AppHost host, SkippyConfig config)
    {
        var skipAll = config.SkipMassivePC;

        var master = Toggle(
            "Skip all of the Large-Scale Content Cutscenes",
            "If you find any sort of cutscene that is skipped by that group, feel free to inform me about it so I can figure out more about how to split the group into exemptions and more.",
            skipAll,
            value =>
            {
                config.SkipMassivePC = value;
                config.SkipCosmicExploration = value;
            },
            host);

        var cosmicChecked = skipAll ? !config.SkipCosmicExploration : config.SkipCosmicExploration;
        var cosmic = Toggle(
            skipAll ? "Exempt: Cosmic Exploration" : "Skip: Cosmic Exploration",
            "Skips cutscenes that trigger within Cosmic Exploration maps. (Sinus Ardorum, Phaenna, Oizys)",
            cosmicChecked,
            value => config.SkipCosmicExploration = skipAll ? !value : value,
            host);

        return new SettingsPageModel
        {
            Title = "Large-Scale Content",
            Intro = "This section covers cutscenes that may be triggered inside large-scale content such as Variant Dungeons or Deep Dungeons, or so I believe.\n"
                    + "This list remains uncertain as I still need to explore what kind of cutscenes are triggered through this signature hook.",
            Sections =
            [
                new SettingsSection { Items = [master] },
                new SettingsSection
                {
                    Header = skipAll ? "[WIP] Exemptions" : "[WIP] Skip Specific",
                    Description = skipAll
                        ? "Any enabled entries in this category will be exempt from the global skip above and cutscenes will play as normal in those instances."
                        : "Any ticked entries in this category will be skipped individually, even if the global skip above is not enabled.",
                    Items = [cosmic],
                },
            ],
        };
    }

    private static SettingsPageModel BuildGoldSaucer(AppHost host, SkippyConfig config)
    {
        var skipAll = config.SkipGoldSaucer;

        var master = Toggle(
            "Skip all of the Gold Saucer Cutscenes",
            "This will skip all cutscenes in all minigames. To specify minigames you wish to keep, use the Exemptions feature below if this setting is enabled, "
            + "or you can skip individual cutscenes below instead if you keep this toggle off.",
            skipAll,
            value =>
            {
                config.SkipGoldSaucer = value;
                config.ExemptChocoboRace = false;
                config.ExemptVerminion = false;
                config.ExemptTripleTriad = false;
                config.ExemptFallGuys = false;
                config.ExemptKeyboundBrawler = false;
                config.ExemptAirForceOne = false;
                config.ExemptMahjong = false;
            },
            host);

        var items = new List<ToggleItem>();

        void Add(string label, string tip, bool value, Action<bool> setter) =>
            items.Add(Toggle(skipAll ? $"Exempt: {label}" : $"Skip: {label}", tip, value, setter, host));

        Add("Mahjong",
            skipAll ? "Cutscenes will play normally during Mahjong." : "Cutscenes will be skipped during Mahjong.",
            config.ExemptMahjong, v => config.ExemptMahjong = v);

        Add("Air Force One",
            skipAll ? "Cutscenes will play normally during Air Force One." : "Cutscenes will be skipped during Air Force One.",
            config.ExemptAirForceOne, v => config.ExemptAirForceOne = v);

        Add("Chocobo Racing",
            skipAll ? "Cutscenes will play normally during Chocobo Racing." : "Cutscenes will be skipped during Chocobo Racing.",
            config.ExemptChocoboRace, v => config.ExemptChocoboRace = v);

        Add("Lord of Verminion",
            skipAll ? "Cutscenes will play normally during Lord of Verminion." : "Cutscenes will be skipped during Lord of Verminion.",
            config.ExemptVerminion, v => config.ExemptVerminion = v);

        Add("Triple Triad (Battlehall / Tournaments)",
            skipAll
                ? "Cutscenes will play normally in Triple Triad Battlehall, Open Tournaments, and Invitational Parlor."
                : "Cutscenes will be skipped in Triple Triad Battlehall, Open Tournaments, and Invitational Parlor.",
            config.ExemptTripleTriad, v => config.ExemptTripleTriad = v);

        Add("Fall Guys Event (Blunderville)",
            skipAll
                ? "Cutscenes will play normally in Blunderville (Fall Guys crossover event)."
                : "Cutscenes will be skipped in Blunderville (Fall Guys crossover event).",
            config.ExemptFallGuys, v => config.ExemptFallGuys = v);

        Add("Keybound Brawler",
            skipAll ? "Cutscenes will play normally during Keybound Brawler." : "Cutscenes will be skipped during Keybound Brawler.",
            config.ExemptKeyboundBrawler, v => config.ExemptKeyboundBrawler = v);

        items.Add(ToggleItem.WorkInProgress(skipAll ? "Exempt: Fashion Report" : "Skip: Fashion Report"));

        return new SettingsPageModel
        {
            Title = "Gold Saucer",
            Intro = "This section covers the cutscenes that play inside the Gold Saucer minigames. "
                    + "I've still yet to test out if anything more is contained in this group, but I do believe all of them should be listed below.",
            Sections =
            [
                new SettingsSection { Items = [master] },
                new SettingsSection
                {
                    Header = skipAll ? "[WIP] Exemptions" : "[WIP] Skip Specific",
                    Description = skipAll
                        ? "There shouldn't be anything extra needed for this category, but feel free to let me know if you notice one that isn't listed or skipped. "
                          + "Any ticked entries in this section will be skipped individually, should you only want to skip certain minigames, otherwise use the global setting above."
                        : "There shouldn't be anything extra needed for this category, but feel free to let me know if you notice one that isn't listed or skipped. "
                          + "Any ticked entries in this category will be skipped individually, in case you want to only skip specific parts of this category and not all of them.",
                    Items = items,
                },
            ],
        };
    }

    private static SettingsPageModel BuildCustomTalk(AppHost host, SkippyConfig config)
    {
        var skipAll = config.SkipCustomTalk;

        var master = Toggle(
            "Skip all of the NPC Dialogue Cutscenes",
            "If you find any cutscene that is skipped by this group, feel free to let me know about it so I can add better ways to split the group into exemptions and more.",
            skipAll,
            value =>
            {
                config.SkipCustomTalk = value;
                config.ExemptSubmarines = false;
            },
            host);

        var submarines = Toggle(
            skipAll ? "Exempt: Submarine & Workshop" : "Skip: Submarine & Workshop",
            skipAll
                ? "Cutscenes will play normally for Submarine & Workshop related content."
                : "Cutscenes will be skipped for Submarine & Workshop related content.",
            config.ExemptSubmarines,
            value => config.ExemptSubmarines = value,
            host);

        return new SettingsPageModel
        {
            Title = "NPC Dialogue Cutscenes",
            Intro = "This section covers cutscenes that are typically triggered via NPC interactions. This includes certain quest NPCs, housing and more.\n"
                    + "This list is still quite uncertain as I still need to figure out all the cutscenes that are skipped via this signature hook.",
            Sections =
            [
                new SettingsSection { Items = [master] },
                new SettingsSection
                {
                    Header = skipAll ? "[WIP] Exemptions" : "[WIP] Skip Specific",
                    Description = skipAll
                        ? "Any enabled entries in this category will be skipped individually in case you want to only skip specific elements, rather than all of them by using the toggle in the above section."
                        : "Any ticked entries in this category will be skipped individually, in case you want to only skip specific parts of this category and not all of them.",
                    Items = [submarines, ToggleItem.WorkInProgress(skipAll ? "Exempt: Grand Company Rankup" : "Skip: Grand Company Rankup")],
                },
            ],
        };
    }

    private static SettingsPageModel BuildNormalCutscenes(AppHost host, SkippyConfig config)
    {
        var skipAll = config.SkipNormalCutscenes;

        var master = Toggle(
            "Skip all of the World & Quest Cutscenes",
            "If you find any cutscene that is skipped by this group, feel free to let me know about it so I can add better ways to split the group into exemptions and more.",
            skipAll,
            value => config.SkipNormalCutscenes = value,
            host);

        return new SettingsPageModel
        {
            Title = "World & Quest Cutscenes",
            Intro = "This section covers cutscenes that are typically triggered during overworld story scenes, side quest cutscenes, and various scripted events.\n"
                    + "This list is still quite uncertain as I still need to figure out all the cutscenes that are skipped via this signature hook.",
            Sections =
            [
                new SettingsSection { Items = [master] },
                new SettingsSection
                {
                    Header = skipAll ? "[WIP] Exemptions" : "[WIP] Skip Specific",
                    Description = "As I have yet to figure out the details and contents of this hook, no further toggles have been finalised just yet.",
                    Items = [ToggleItem.WorkInProgress(skipAll ? "Exempt: (Unknown)" : "Skip: (Unknown)")],
                },
            ],
        };
    }

    private static SettingsPageModel BuildFeedBuddy(AppHost host, SkippyConfig config)
    {
        var master = Toggle(
            "Skip Feed Buddy Cutscene",
            "Not much to say there, a bit sad you don't want to see that cute animation :(",
            config.SkipFeedBuddy,
            value => config.SkipFeedBuddy = value,
            host);

        return new SettingsPageModel
        {
            Title = "Feed Buddy Animation",
            Intro = "This section covers the Companion Feeding animation cutscene that plays when you feed your Chocobo in a Chocobo Stable in housing districts.",
            Sections = [new SettingsSection { Items = [master] }],
        };
    }

    private static SettingsPageModel BuildRiskySkips(AppHost host, SkippyConfig config) => new()
    {
        Title = "Risky Skips",
        ShowRiskyBanner = true,
        Intro = string.Empty,
        Sections =
        [
            new SettingsSection
            {
                Header = "Ocean Fishing",
                Description = "The very first cutscene in Ocean Fishing allows you to start directly when skipped, however the next two will only let you move around the boat, "
                              + "though the timer won't start (so on not letting you fish), until the moment everyone is ready.",
                Items =
                [
                    Toggle("Enable Ocean Fishing Skip",
                        "Skips cutscenes during Ocean Fishing. Enable at your own discretion and be aware of the risks.",
                        config.SkipOceanFishing, value => config.SkipOceanFishing = value, host),
                ],
            },
            new SettingsSection
            {
                Header = "Crystalline Conflict",
                Description = "When loading into Crystalline Conflict, the camera pans around the entire map then displays each team one by one and shows the player cards of everyone in the lobby, "
                              + "this skip allows you to skip that entirely.",
                Items =
                [
                    Toggle("Enable Crystalline Conflict Skip",
                        "Skips the intro cutscene in Crystalline Conflict. Enable at your own discretion and be aware of the risks.",
                        config.SkipCrystallineConflict, value => config.SkipCrystallineConflict = value, host),
                ],
            },
            new SettingsSection
            {
                Header = "Inn Skip",
                Description = "This skip is by far the riskiest the game \"tolerates\", as it completely bypasses the Inn Login Sequence.",
                Items =
                [
                    Toggle("Enable Inn Skip",
                        "Bypasses the entire Inn Login Sequence. Enable at your own discretion and be aware of the risks.",
                        config.SkipInn, value => config.SkipInn = value, host),
                ],
            },
            new SettingsSection
            {
                Header = "[WIP] Aesthetician Skip",
                Description = "This will someday allow you to skip the cutscene that plays when visiting the Aesthetician for a hair or appearance change.\n"
                              + "It has however not yet been identified, so this feature remains a work in progress.",
                Items = [ToggleItem.WorkInProgress("Enable Aesthetician Skip")],
            },
        ],
    };

    private static SettingsPageModel BuildAutoParty(AppHost host, SkippyConfig config)
    {
        var master = Toggle(
            "Auto-Enable MSQ Roulette Skip in 4-man Party",
            "When your queue pops for Castrum Meridianum, The Praetorium, Porta Decumana, or MSQ Roulette while in a premade party of 4, "
            + "the skip will be enabled automatically. It will disable itself again once you leave the instance.",
            config.AutoEnable4Man,
            value =>
            {
                config.AutoEnable4Man = value;
                if (value)
                {
                    config.SkipMSQRoulette = false;
                    config.ExemptPrae = false;
                    config.ExemptCastrum = false;
                    config.ExemptPorta = false;
                }
                else
                {
                    config.CheckPartySkippy = false;
                }

                host.ResetAutoParty();
                host.Notify(
                    value
                        ? "Auto-Party Mode Enabled - The skip will now only activate when entering MSQ duties."
                        : "Auto-Party Mode Disabled - The skip will no longer activate automatically.",
                    Services.ToastKind.Info);
            },
            host);

        var checkParty = Toggle(
            "Check Party Members for Skippy",
            "Only enable the skip when every party member has Skippy installed. Requires Auto-Party Mode to function.",
            config.CheckPartySkippy,
            value => config.CheckPartySkippy = value,
            host);

        checkParty.IsEnabled = config.AutoEnable4Man;

        return new SettingsPageModel
        {
            Title = "Auto-Party Mode",
            Intro = "When you enter an MSQ duty with a premade party of 4, the MSQ Roulette Skip will be enabled automatically. "
                    + "Once you leave the instance, it will be disabled again — that way, you don't have to take any risk of skipping cutscenes without people you trust!",
            Sections =
            [
                new SettingsSection { Items = [master] },
                new SettingsSection
                {
                    Header = "Check Party Members for Skippy",
                    Description = "When enabled, Skippy will verify that ALL members of your party also have Skippy installed before enabling the skip.\n\n"
                                  + "How it works: when entering the duty, each player with Skippy posts an anonymous request. "
                                  + "After a short wait, Skippy checks whether every member of the party posted one or not. "
                                  + "If they all did, it is safe to skip; if even one player is missing, the skip will NOT be enabled for this run."
                                  + (config.AutoEnable4Man
                                      ? string.Empty
                                      : "\n\n⚠ This feature requires Auto-Party Mode to be enabled in order to function. Enable it in the section above first."),
                    Items = [checkParty],
                },
            ],
        };
    }

    private static SettingsPageModel BuildBehaviour(AppHost host, SkippyConfig config) => new()
    {
        Title = "Behaviour",
        Intro = "The game possesses a function called IsCutsceneSeen which determines whether a cutscene has already been viewed by the player. "
                + "When it returns true, the game treats the cutscene as already seen and skips it — this is the same mechanism the game itself uses to skip cutscenes you have already watched.\n\n"
                + "Skippy hooks into that function so that, when a relevant skip category is active, it skips the cutscene as if you had already seen it. "
                + "This is what allows Skippy to skip cutscenes that are otherwise marked as unskippable.\n\n"
                + "The option below is an opt-in that removes the first-time protection. With it enabled, every cutscene in the game, including ones you have never seen before, "
                + "may be skipped by Skippy at any time. Only enable it if you are certain you do not mind losing the first-time viewing experience.",
        Sections =
        [
            new SettingsSection
            {
                Header = "First-Time Viewing",
                Items =
                [
                    Toggle("Disable your first-time viewing experience",
                        "Opts in to allowing any cutscene in the game to be skipped. This may lead to cutscenes you've never seen being skipped.",
                        config.AllowCutsceneSeenGlobally, value => config.AllowCutsceneSeenGlobally = value, host),
                ],
            },
        ],
    };

    private static SettingsPageModel BuildHookResearch(AppHost host, SkippyConfig config)
    {
        ToggleItem Research(string label, string tip, string signature, bool value, Action<bool> setter)
        {
            var item = Toggle(label, tip + "\n\nSignature:\n" + signature, value, setter, host);
            item.IsEnabled = config.DevMode;
            item.Suffix = "[SigHook]";
            return item;
        }

        return new SettingsPageModel
        {
            Title = "Hook Research",
            Intro = "This section allows you to test the various game hooks used by Skippy. Each toggle enables a signature hook in isolation, bypassing all exemption logic otherwise present within the plugin. "
                    + "If a hook fires, it will be logged and sent to the research sheet, with the hook name, territory ID, zone name, and intended use.\n\n"
                    + "Please report any found data back to Boxu so we can improve this plugin by adding more to it that may come of use to players.",
            Sections =
            [
                new SettingsSection
                {
                    Header = "Research Hooks",
                    Items =
                    [
                        Research("MSQ Roulette Hook", "Enables the ContentDirector_IsPlayCutscene hook globally, without any exemptions.",
                            "48 89 5C 24 ?? 57 48 83 EC 50 ... 48 8B 08 84 99 ?? ?? ?? ??", config.ResearchMSQHook,
                            v => config.ResearchMSQHook = v),
                        Research("Large-Scale Content Hook", "Enables the MassivePcContentDirector_IsPlayCutscene hook globally, without any exemptions.",
                            "48 89 5C 24 ?? 57 48 83 EC 50 ... 48 8B 08 48 8B 11", config.ResearchMassivePCHook,
                            v => config.ResearchMassivePCHook = v),
                        Research("Gold Saucer Hook", "Enables the GoldSaucerDirector_IsPlayCutscene hook globally, without any exemptions.",
                            "48 89 5C 24 ?? 57 48 83 EC 50 ... 48 8B 08 84 99 ?? ?? ?? ??", config.ResearchGoldSaucerHook,
                            v => config.ResearchGoldSaucerHook = v),
                        Research("NPC Dialogue Hook", "Enables the CustomTalk_IsPlayCutsceneContent hook globally, without any exemptions.",
                            "48 83 EC 58 48 8B D1 ... 48 8B 08 48 85 C9 74 06", config.ResearchCustomTalkHook,
                            v => config.ResearchCustomTalkHook = v),
                        Research("World Cutscenes Hook", "Enables the EventSceneModuleUsualImpl_PlayCutScene hook globally, without any exemptions.",
                            "40 53 55 57 41 56 48 81 EC ?? ?? ?? ?? ... 48 8B 59 08", config.ResearchNormalCutscenesHook,
                            v => config.ResearchNormalCutscenesHook = v),
                        Research("Inn Hook", "Enables the IsEnterTerritoryEventLogin hook globally, without any exemptions.",
                            "48 83 EC 58 48 8B D1 ... 48 8B 4C 24 ??", config.ResearchInnHook,
                            v => config.ResearchInnHook = v),
                        Research("Feed Buddy Hook", "Enables the PlayFeedBuddyScene hook globally, without any exemptions.",
                            "48 8B 49 08 48 85 C9 74 0C ... 48 8B 41 08", config.ResearchFeedBuddyHook,
                            v => config.ResearchFeedBuddyHook = v),
                        Toggle("Extra Logs", "Enables verbose research logging.",
                            config.ResearchExtraLogs, v => config.ResearchExtraLogs = v, host),
                    ],
                },
                new SettingsSection
                {
                    Header = "New Hooks Exploration",
                    Description = "This section contains experimental and unconfirmed hooks that may not work as intended.",
                    Items =
                    [
                        Research("Grand Company Rank-Up Hook", "Enables the EventSceneModuleUsualImpl_GrandCompanyRankUp hook globally, without any exemptions.",
                            "40 53 48 83 EC 50 48 8B D9 ... 48 8D 6C 24 ??", config.ResearchGrandCompanyRankUpHook,
                            v => config.ResearchGrandCompanyRankUpHook = v),
                        Research("Aesthetician Hook", "Enables the EventSceneModuleUsualImpl_HairMake hook globally, without any exemptions.",
                            "48 89 5C 24 ?? 57 48 83 EC 50 48 8B D9 ... 48 85 C0", config.ResearchHairMakeHook,
                            v => config.ResearchHairMakeHook = v),
                    ],
                },
            ],
        };
    }
}

