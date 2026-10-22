using System;
using System.Collections.Generic;
using System.Globalization;
using Order.Contracts;

namespace Order.Content
{
    public enum OptionKind { Toggle, Range, Choice, Key }

    // One menu parameter. Aliases are lowercase regex fragments (ё written as е) used by text commands.
    public sealed class MenuOption
    {
        public readonly string Id;
        public readonly AppScreen Tab;
        public readonly string Section;
        public readonly string Title;
        public readonly string Description;
        public readonly OptionKind Kind;
        public readonly float Min, Max, Default;
        public readonly IReadOnlyList<string> Choices;
        public readonly IReadOnlyList<string> ChoiceAliases;
        public readonly IReadOnlyCollection<int> Locked;
        public readonly string DefaultKey;
        public readonly string Aliases;
        // Live options change the running application; the rest are stored until their systems exist.
        public readonly bool Live;
        public readonly bool DevelopmentOnly;
        public readonly AppScreen? AlsoOn;

        public MenuOption(string id, AppScreen tab, string section, string title, string description, OptionKind kind, string aliases,
            float min = 0, float max = 100, float defaultValue = 0, string[] choices = null, string[] choiceAliases = null, int[] locked = null,
            string defaultKey = null, bool live = false, bool developmentOnly = false, AppScreen? alsoOn = null)
        {
            Id = id; Tab = tab; Section = section; Title = title; Description = description; Kind = kind; Aliases = aliases;
            Min = min; Max = max; Default = defaultValue; Choices = choices ?? Array.Empty<string>(); ChoiceAliases = choiceAliases;
            Locked = locked ?? Array.Empty<int>(); DefaultKey = defaultKey; Live = live; DevelopmentOnly = developmentOnly; AlsoOn = alsoOn;
        }
    }

    public sealed class MenuTab
    {
        public readonly AppScreen Screen;
        public readonly string Title;
        public readonly string Note;
        public readonly string Aliases;
        public MenuTab(AppScreen screen, string title, string note, string aliases) { Screen = screen; Title = title; Note = note; Aliases = aliases; }
    }

    // Every parameter of the main menu and options window, shared by the window, the application and text commands.
    public static class MenuCatalog
    {
        public const string MasterVolume = "audio.master";
        public const string AssistantImage = "voice.assistant";
        public const string InterfaceVolume = "audio.interface";
        public const string TacticalCursor = "ui.cursor";
        public const string Scanlines = "ui.scanlines";
        public const string Diagnostics = "voice.diagnostics";
        public const string Difficulty = "match.difficulty";

        public static readonly IReadOnlyList<MenuTab> Tabs = new[]
        {
            new MenuTab(AppScreen.GameSettings, "ИГРА", "Параметры вкладки — заглушки: соответствующие системы ещё в разработке. Значения сохраняются.", @"игр\w*|общ\w*"),
            new MenuTab(AppScreen.ControlsSettings, "УПРАВЛЕНИЕ", "Назначения клавиш и камера пока не применяются: схема управления матча ещё формируется.", @"управлени\w*|клавиш\w*|камер\w*|мыш\w*"),
            new MenuTab(AppScreen.GraphicsSettings, "ГРАФИКА", "Графические параметры пока не применяются: значения сохраняются до появления систем.", @"график\w*|видео|изображени\w*"),
            new MenuTab(AppScreen.AudioSettings, "ЗВУК", "Действуют параметры с отметкой «ДЕЙСТВУЕТ». Остальные каналы появятся вместе со звуком игры.", @"звук\w*|аудио"),
            new MenuTab(AppScreen.VoiceSettings, "ГОЛОС И АССИСТЕНТ", "Действуют параметры с отметкой «ДЕЙСТВУЕТ». Микрофон и озвучка подключаются следующим этапом.", @"голос\w*|ассистент\w*|микрофон\w*|словар\w*"),
            new MenuTab(AppScreen.InterfaceSettings, "ИНТЕРФЕЙС", "Действуют параметры с отметкой «ДЕЙСТВУЕТ».", @"интерфейс\w*|оформлени\w*|hud|худ"),
        };

        private static readonly string[] Quality3 = { "Низкие", "Средние", "Высокие" };
        private static readonly string[] Quality3Aliases = { @"низк\w*", @"средн\w*", @"высок\w*" };

        public static readonly IReadOnlyList<MenuOption> Options = new[]
        {
            new MenuOption("game.language", AppScreen.GameSettings, "ОБЩЕЕ", "Язык интерфейса", "Язык меню, подсказок и ответов ассистента. Другие языки появятся позже.",
                OptionKind.Choice, @"язык\w*", choices: new[] { "Русский" }, choiceAliases: new[] { @"русск\w*" }),
            new MenuOption("game.speed", AppScreen.GameSettings, "ОБЩЕЕ", "Скорость игры", "Темп симуляции в матчах против ИИ.",
                OptionKind.Choice, @"скорост\w* игры|темп\w* игры", defaultValue: 2, choices: new[] { "Нормальная", "Быстрая", "Очень быстрая" },
                choiceAliases: new[] { @"нормальн\w*|обычн\w*", @"(?<!очень )быстр\w*", @"очень быстр\w*|быстрее|максимальн\w*" }),
            new MenuOption("game.tips", AppScreen.GameSettings, "ПОДСКАЗКИ И МАТЧ", "Обучающие подсказки", "Короткие пояснения механик при первом знакомстве.",
                OptionKind.Toggle, @"обучающ\w* подсказк\w*|подсказк\w*", defaultValue: 1),
            new MenuOption("game.confirmExit", AppScreen.GameSettings, "ПОДСКАЗКИ И МАТЧ", "Подтверждать выход из матча", "Запрашивать подтверждение перед сдачей или выходом.",
                OptionKind.Toggle, @"подтвержд\w* выход\w*", defaultValue: 1),
            new MenuOption("game.replays", AppScreen.GameSettings, "ПОДСКАЗКИ И МАТЧ", "Сохранять повторы", "Автоматически записывать повтор каждого матча.",
                OptionKind.Toggle, @"(?:запис\w* |сохран\w* )?повтор\w*", defaultValue: 1),

            new MenuOption("controls.cameraSpeed", AppScreen.ControlsSettings, "КАМЕРА И МЫШЬ", "Скорость камеры", "Скорость перемещения камеры клавишами и у края экрана.",
                OptionKind.Range, @"скорост\w* камер\w*", defaultValue: 50),
            new MenuOption("controls.edgeScroll", AppScreen.ControlsSettings, "КАМЕРА И МЫШЬ", "Прокрутка у края экрана", "Камера сдвигается, когда курсор упирается в край экрана.",
                OptionKind.Toggle, @"прокрутк\w*(?: у кра\w*(?: экрана)?)?|кра\w* экрана", defaultValue: 1),
            new MenuOption("controls.invertZoom", AppScreen.ControlsSettings, "КАМЕРА И МЫШЬ", "Инвертировать масштаб колесом", "Меняет направление приближения колесом мыши.",
                OptionKind.Toggle, @"инвер\w*(?: масштаб\w*)?(?: колес\w*)?"),
            new MenuOption("controls.mouse", AppScreen.ControlsSettings, "КАМЕРА И МЫШЬ", "Чувствительность мыши", "Скорость курсора в матче.",
                OptionKind.Range, @"чувствительност\w* мыш\w*", defaultValue: 50),
            new MenuOption("key.talk", AppScreen.ControlsSettings, "КЛАВИШИ", "Говорить с ассистентом", "Удерживайте, чтобы отдать голосовой приказ.",
                OptionKind.Key, @"кнопк\w* разговор\w*|клавиш\w* разговор\w*|разговор\w*|говорить с ассистентом", defaultKey: "V", alsoOn: AppScreen.VoiceSettings),
            new MenuOption("key.cameraUp", AppScreen.ControlsSettings, "КЛАВИШИ", "Камера вперёд", "Перемещение камеры.", OptionKind.Key, @"камер\w* вперед", defaultKey: "W"),
            new MenuOption("key.cameraDown", AppScreen.ControlsSettings, "КЛАВИШИ", "Камера назад", "Перемещение камеры.", OptionKind.Key, @"камер\w* назад", defaultKey: "S"),
            new MenuOption("key.cameraLeft", AppScreen.ControlsSettings, "КЛАВИШИ", "Камера влево", "Перемещение камеры.", OptionKind.Key, @"камер\w* влево", defaultKey: "A"),
            new MenuOption("key.cameraRight", AppScreen.ControlsSettings, "КЛАВИШИ", "Камера вправо", "Перемещение камеры.", OptionKind.Key, @"камер\w* вправо", defaultKey: "D"),
            new MenuOption("key.focusWorker", AppScreen.ControlsSettings, "КЛАВИШИ", "Камера к рабочему", "Центрировать камеру на выбранном рабочем.",
                OptionKind.Key, @"камер\w* к рабоч\w*|к рабоч\w*", defaultKey: "ПРОБЕЛ"),
            new MenuOption("key.build", AppScreen.ControlsSettings, "КЛАВИШИ", "Строительство", "Открыть размещение здания.", OptionKind.Key, @"строительств\w*|стройк\w*", defaultKey: "B"),

            new MenuOption("gfx.mode", AppScreen.GraphicsSettings, "ЭКРАН", "Режим экрана", "Способ вывода изображения.",
                OptionKind.Choice, @"режим\w* экрана|полноэкранн\w*(?: режим\w*)?|оконн\w* режим\w*",
                choices: new[] { "Полный экран", "Окно без рамки", "Окно" }, choiceAliases: new[] { @"полн\w* экран\w*|полноэкранн\w*", @"без рамк\w*", @"окн\w*|оконн\w*" }),
            new MenuOption("gfx.resolution", AppScreen.GraphicsSettings, "ЭКРАН", "Разрешение", "Внутреннее разрешение вывода.",
                OptionKind.Choice, @"разрешени\w*", defaultValue: 3,
                choices: new[] { "1280 × 720", "1366 × 768", "1600 × 900", "1920 × 1080", "2560 × 1440", "3840 × 2160" },
                choiceAliases: new[] { @"1280\D{0,5}720|720p", @"1366\D{0,5}768", @"1600\D{0,5}900", @"1920\D{0,5}1080|1080p|full ?hd", @"2560\D{0,5}1440|1440p|2k", @"3840\D{0,5}2160|2160p|4k" }),
            new MenuOption("gfx.vsync", AppScreen.GraphicsSettings, "ЭКРАН", "Вертикальная синхронизация", "Убирает разрывы кадра ценой небольшой задержки.",
                OptionKind.Toggle, @"вертикальн\w* синхронизац\w*|синхронизац\w*|v-?sync|всинк\w*"),
            new MenuOption("gfx.fps", AppScreen.GraphicsSettings, "ЭКРАН", "Ограничение кадров", "Максимальная частота кадров.",
                OptionKind.Choice, @"ограничени\w* (?:частоты )?кадр\w*|частот\w* кадр\w*|fps|фпс",
                choices: new[] { "Без ограничения", "60", "120", "144", "240" },
                choiceAliases: new[] { @"без ограничени\w*|безлимит\w*|выкл\w*|нет", @"\b60\b", @"\b120\b", @"\b144\b", @"\b240\b" }),
            new MenuOption("gfx.brightness", AppScreen.GraphicsSettings, "ЭКРАН", "Яркость", "Общая яркость изображения.", OptionKind.Range, @"яркост\w*", defaultValue: 50),
            new MenuOption("gfx.preset", AppScreen.GraphicsSettings, "КАЧЕСТВО", "Общий пресет", "Быстрый выбор набора параметров.",
                OptionKind.Choice, @"(?:общ\w* )?пресет\w*|(?:общ\w* )?качеств\w*(?: графики)?", defaultValue: 2,
                choices: new[] { "Низкое", "Среднее", "Высокое", "Ультра" }, choiceAliases: new[] { @"низк\w*", @"средн\w*", @"высок\w*", @"ультр\w*|максимал\w*" }),
            new MenuOption("gfx.shadows", AppScreen.GraphicsSettings, "КАЧЕСТВО", "Тени", "Качество и дальность теней.",
                OptionKind.Choice, @"тен\w*", defaultValue: 2,
                choices: new[] { "Выключены", "Низкие", "Средние", "Высокие" }, choiceAliases: new[] { @"выкл\w*|отключ\w*|без тен\w*|нет", @"низк\w*", @"средн\w*", @"высок\w*" }),
            new MenuOption("gfx.aa", AppScreen.GraphicsSettings, "КАЧЕСТВО", "Сглаживание", "Сглаживание краёв объектов.",
                OptionKind.Choice, @"сглаживани\w*|антиалиас\w*", defaultValue: 2,
                choices: new[] { "Выключено", "FXAA", "SMAA", "MSAA 4×" }, choiceAliases: new[] { @"выкл\w*|отключ\w*|нет", @"fxaa", @"smaa", @"msaa" }),
            new MenuOption("gfx.effects", AppScreen.GraphicsSettings, "КАЧЕСТВО", "Эффекты", "Частицы, взрывы и следы приказов.",
                OptionKind.Choice, @"(?:графическ\w* |визуальн\w* )?эффект\w*(?: графики)?|частиц\w*", defaultValue: 2, choices: Quality3, choiceAliases: Quality3Aliases),
            new MenuOption("gfx.renderScale", AppScreen.GraphicsSettings, "КАЧЕСТВО", "Масштаб рендера", "Внутреннее разрешение отрисовки мира.",
                OptionKind.Range, @"масштаб\w* рендер\w*|рендер\w*", min: 50, defaultValue: 100),

            new MenuOption(MasterVolume, AppScreen.AudioSettings, "ГРОМКОСТЬ", "Общая громкость", "Общий уровень звука. Также: «громкость 50%», «потише».",
                OptionKind.Range, @"(?:общ\w* )?громкост\w*(?: звука)?|уровень звука", defaultValue: 100, live: true),
            new MenuOption(InterfaceVolume, AppScreen.AudioSettings, "ГРОМКОСТЬ", "Звуки интерфейса", "Отклик кнопок и ответов ассистента в меню.",
                OptionKind.Range, @"(?:звук\w*|громкост\w*) интерфейс\w*", defaultValue: 60, live: true),
            new MenuOption("audio.music", AppScreen.AudioSettings, "ГРОМКОСТЬ", "Музыка", "Громкость музыки.", OptionKind.Range, @"(?:громкост\w* )?музык\w*", defaultValue: 70),
            new MenuOption("audio.effects", AppScreen.AudioSettings, "ГРОМКОСТЬ", "Эффекты", "Звуки боя, строительства и юнитов.",
                OptionKind.Range, @"(?:громкост\w* |звуков\w* )?эффект\w*|звук\w* эффект\w*", defaultValue: 80),
            new MenuOption("audio.voice", AppScreen.AudioSettings, "ГРОМКОСТЬ", "Голос ассистента", "Громкость озвучки ответов.",
                OptionKind.Range, @"(?:громкост\w* )?голос\w* ассистент\w*", defaultValue: 90),
            new MenuOption("audio.speak", AppScreen.AudioSettings, "ПРОЧЕЕ", "Озвучивать ответы", "Ассистент отвечает голосом выбранной фракции.",
                OptionKind.Toggle, @"озвучк\w*(?: ответ\w*)?|озвучива\w*(?: ответ\w*)?", defaultValue: 1),
            new MenuOption("audio.background", AppScreen.AudioSettings, "ПРОЧЕЕ", "Звук в фоне", "Не приглушать звук, когда окно игры неактивно.",
                OptionKind.Toggle, @"звук\w* в фон\w*"),

            new MenuOption(AssistantImage, AppScreen.VoiceSettings, "АССИСТЕНТ", "Образ ассистента", "Облик, голос и манера ответов. Набор функций у образов одинаковый.",
                OptionKind.Choice, @"образ\w* ассистент\w*|ассистент\w*", choices: new[] { "Фракция 1", "Фракция 2" },
                choiceAliases: new[] { @"перв\w*|\b1\b", @"втор\w*|\b2\b" }, live: true),
            new MenuOption("voice.subtitles", AppScreen.VoiceSettings, "АССИСТЕНТ", "Показывать текст ответов", "Дублировать ответы ассистента текстом.",
                OptionKind.Toggle, @"текст\w* ответ\w*|субтитр\w*", defaultValue: 1),
            new MenuOption("voice.mode", AppScreen.VoiceSettings, "МИКРОФОН", "Режим микрофона", "Постоянное прослушивание с обращением появится позже.",
                OptionKind.Choice, @"режим\w* микрофон\w*|режим\w* прослушивани\w*", choices: new[] { "Удержание", "Постоянный" },
                choiceAliases: new[] { @"удержани\w*|по кнопк\w*", @"постоянн\w*|всегда" }, locked: new[] { 1 }),
            new MenuOption("voice.device", AppScreen.VoiceSettings, "МИКРОФОН", "Устройство ввода", "Микрофон для голосовых приказов.",
                OptionKind.Choice, @"устройств\w* ввода|микрофон\w*", choices: new[] { "Системное по умолчанию" }, choiceAliases: new[] { @"систем\w*|по умолчани\w*" }),
            new MenuOption("voice.threshold", AppScreen.VoiceSettings, "МИКРОФОН", "Порог срабатывания", "Чувствительность определения начала речи.",
                OptionKind.Range, @"порог\w*(?: срабатывани\w*)?|чувствительност\w* микрофон\w*", defaultValue: 35),
            new MenuOption(Diagnostics, AppScreen.VoiceSettings, "ДИАГНОСТИКА", "Задержки разбора команд",
                "Строка с замером этапов под консолью приказов. Только в редакторе и Development Build.",
                OptionKind.Toggle, @"диагностик\w*|задержк\w*(?: разбора)?", defaultValue: 1, live: true, developmentOnly: true),

            new MenuOption(TacticalCursor, AppScreen.InterfaceSettings, "ОФОРМЛЕНИЕ", "Тактический курсор", "Курсор в стиле интерфейса вместо системного.",
                OptionKind.Toggle, @"(?:тактическ\w* )?курсор\w*", defaultValue: 1, live: true),
            new MenuOption(Scanlines, AppScreen.InterfaceSettings, "ОФОРМЛЕНИЕ", "Сканирующая линия", "Медленная световая полоса на фоне меню.",
                OptionKind.Toggle, @"сканирующ\w* лини\w*|сканлайн\w*|скан\w* лини\w*", defaultValue: 1, live: true),
            new MenuOption("ui.scale", AppScreen.InterfaceSettings, "ОФОРМЛЕНИЕ", "Масштаб интерфейса", "Размер элементов интерфейса.",
                OptionKind.Range, @"масштаб\w* интерфейс\w*", min: 80, max: 130, defaultValue: 100),
            new MenuOption("ui.motion", AppScreen.InterfaceSettings, "ОФОРМЛЕНИЕ", "Анимации интерфейса", "Плавные переходы и движение элементов.",
                OptionKind.Toggle, @"анимаци\w*(?: интерфейс\w*)?", defaultValue: 1),
            new MenuOption("ui.hudTheme", AppScreen.InterfaceSettings, "HUD МАТЧА", "Оформление HUD", "В матче интерфейс принимает стиль выбранной фракции.",
                OptionKind.Choice, @"оформлени\w* (?:hud|худ)|тем\w* (?:hud|худ)|цвет\w* (?:hud|худ)", choices: new[] { "По фракции игрока", "Командное" },
                choiceAliases: new[] { @"фракци\w*", @"командн\w*" }),
            new MenuOption("ui.grid", AppScreen.InterfaceSettings, "HUD МАТЧА", "Координатная сетка", "Подписи квадратов на миникарте.",
                OptionKind.Toggle, @"координатн\w* сетк\w*|сетк\w*", defaultValue: 1),

            new MenuOption(Difficulty, AppScreen.MatchSetup, "СХВАТКА", "Сложность", "Сложность бота. Пока доступна только обычная.",
                OptionKind.Choice, @"сложност\w*(?: бота)?", defaultValue: 1, choices: new[] { "Лёгкая", "Обычная", "Высокая" },
                choiceAliases: new[] { @"легк\w*|прост\w*", @"обычн\w*|нормальн\w*|средн\w*", @"высок\w*|сложн\w*|тяжел\w*" }, locked: new[] { 0, 2 }),
        };

        private static readonly Dictionary<string, MenuOption> ById = new Dictionary<string, MenuOption>();

        static MenuCatalog() { foreach (var option in Options) ById[option.Id] = option; }

        public static MenuOption Find(string id) => id != null && ById.TryGetValue(id, out var option) ? option : null;

        public static MenuTab Tab(AppScreen screen)
        {
            foreach (var tab in Tabs) if (tab.Screen == screen) return tab;
            return null;
        }

        public static string ScreenTitle(AppScreen screen)
        {
            if (screen == AppScreen.MainMenu) return "штаб";
            if (screen == AppScreen.MatchSetup) return "схватка с ИИ";
            var tab = Tab(screen);
            return tab == null ? "меню" : "настройки — " + tab.Title.ToLowerInvariant();
        }

        public static float Value(SettingsSnapshot settings, MenuOption option) =>
            Value(option, settings.MasterVolume, settings.AssistantFaction, settings.Options);

        public static float Value(AppCommandState state, MenuOption option) =>
            Value(option, state.MasterVolume, state.AssistantFaction, state.Options);

        private static float Value(MenuOption option, float masterVolume, FactionId assistant, IReadOnlyDictionary<string, float> options)
        {
            if (option.Id == MasterVolume) return masterVolume * 100;
            if (option.Id == AssistantImage) return (int)assistant;
            return options.TryGetValue(option.Id, out var value) ? value : option.Default;
        }

        public static string Key(IReadOnlyDictionary<string, string> keys, MenuOption option) =>
            keys.TryGetValue(option.Id, out var key) ? key : option.DefaultKey;

        public static string Format(MenuOption option, float value)
        {
            switch (option.Kind)
            {
                case OptionKind.Toggle: return value >= .5f ? "вкл" : "выкл";
                case OptionKind.Range: return Math.Round(value).ToString(CultureInfo.InvariantCulture) + "%";
                case OptionKind.Choice:
                    var index = (int)Math.Round(value);
                    return index >= 0 && index < option.Choices.Count ? option.Choices[index] : "—";
                default: return "";
            }
        }

        public static bool IsValid(MenuOption option, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return false;
            switch (option.Kind)
            {
                case OptionKind.Toggle: return value == 0 || value == 1;
                case OptionKind.Range: return value >= option.Min && value <= option.Max;
                case OptionKind.Choice: return value == Math.Round(value) && value >= 0 && value < option.Choices.Count;
                default: return false;
            }
        }
    }
}
