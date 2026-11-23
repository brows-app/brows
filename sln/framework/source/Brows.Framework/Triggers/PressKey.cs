namespace Brows.Triggers;

/// <summary>
/// Identifies a keyboard key independently of the current keyboard layout's text output.
/// </summary>
public enum PressKey {
    /// <summary>
    /// No key.
    /// </summary>
    None = 0,

    /// <summary>
    /// The Cancel key.
    /// </summary>
    Cancel = 1,

    /// <summary>
    /// The Backspace key.
    /// </summary>
    Back = 2,

    /// <summary>
    /// The Tab key.
    /// </summary>
    Tab = 3,

    /// <summary>
    /// The Line Feed key.
    /// </summary>
    LineFeed = 4,

    /// <summary>
    /// The Clear key.
    /// </summary>
    Clear = 5,

    /// <summary>
    /// The Enter key.
    /// </summary>
    Enter = 6,

    /// <summary>
    /// The Return key; an alias for Enter.
    /// </summary>
    Return = 6,

    /// <summary>
    /// The Pause key.
    /// </summary>
    Pause = 7,

    /// <summary>
    /// The Caps Lock key.
    /// </summary>
    Capital = 8,

    /// <summary>
    /// The Caps Lock key; an alias for Capital.
    /// </summary>
    CapsLock = 8,

    /// <summary>
    /// The Hangul input-mode key.
    /// </summary>
    HangulMode = 9,

    /// <summary>
    /// The Kana input-mode key; shares its value with HangulMode.
    /// </summary>
    KanaMode = 9,

    /// <summary>
    /// The Junja input-mode key.
    /// </summary>
    JunjaMode = 10,

    /// <summary>
    /// The Final input-mode key.
    /// </summary>
    FinalMode = 11,

    /// <summary>
    /// The Hanja input-mode key.
    /// </summary>
    HanjaMode = 12,

    /// <summary>
    /// The Kanji input-mode key; shares its value with HanjaMode.
    /// </summary>
    KanjiMode = 12,

    /// <summary>
    /// The Escape key.
    /// </summary>
    Escape = 13,

    /// <summary>
    /// The input method's Convert key.
    /// </summary>
    ImeConvert = 14,

    /// <summary>
    /// The input method's Nonconvert key.
    /// </summary>
    ImeNonConvert = 15,

    /// <summary>
    /// The input method's Accept key.
    /// </summary>
    ImeAccept = 16,

    /// <summary>
    /// The input method's mode-change key.
    /// </summary>
    ImeModeChange = 17,

    /// <summary>
    /// The Space key.
    /// </summary>
    Space = 18,

    /// <summary>
    /// The Page Up key.
    /// </summary>
    PageUp = 19,

    /// <summary>
    /// The Page Up key; an alias for PageUp.
    /// </summary>
    Prior = 19,

    /// <summary>
    /// The Page Down key.
    /// </summary>
    Next = 20,

    /// <summary>
    /// The Page Down key; an alias for Next.
    /// </summary>
    PageDown = 20,

    /// <summary>
    /// The End key.
    /// </summary>
    End = 21,

    /// <summary>
    /// The Home key.
    /// </summary>
    Home = 22,

    /// <summary>
    /// The left arrow key.
    /// </summary>
    Left = 23,

    /// <summary>
    /// The up arrow key.
    /// </summary>
    Up = 24,

    /// <summary>
    /// The right arrow key.
    /// </summary>
    Right = 25,

    /// <summary>
    /// The down arrow key.
    /// </summary>
    Down = 26,

    /// <summary>
    /// The Select key.
    /// </summary>
    Select = 27,

    /// <summary>
    /// The Print key.
    /// </summary>
    Print = 28,

    /// <summary>
    /// The Execute key.
    /// </summary>
    Execute = 29,

    /// <summary>
    /// The Print Screen key.
    /// </summary>
    PrintScreen = 30,

    /// <summary>
    /// The Print Screen key; an alias for PrintScreen.
    /// </summary>
    Snapshot = 30,

    /// <summary>
    /// The Insert key.
    /// </summary>
    Insert = 31,

    /// <summary>
    /// The Delete key.
    /// </summary>
    Delete = 32,

    /// <summary>
    /// The Help key.
    /// </summary>
    Help = 33,

    /// <summary>
    /// The 0 key above the letter keys.
    /// </summary>
    D0 = 34,

    /// <summary>
    /// The 1 key above the letter keys.
    /// </summary>
    D1 = 35,

    /// <summary>
    /// The 2 key above the letter keys.
    /// </summary>
    D2 = 36,

    /// <summary>
    /// The 3 key above the letter keys.
    /// </summary>
    D3 = 37,

    /// <summary>
    /// The 4 key above the letter keys.
    /// </summary>
    D4 = 38,

    /// <summary>
    /// The 5 key above the letter keys.
    /// </summary>
    D5 = 39,

    /// <summary>
    /// The 6 key above the letter keys.
    /// </summary>
    D6 = 40,

    /// <summary>
    /// The 7 key above the letter keys.
    /// </summary>
    D7 = 41,

    /// <summary>
    /// The 8 key above the letter keys.
    /// </summary>
    D8 = 42,

    /// <summary>
    /// The 9 key above the letter keys.
    /// </summary>
    D9 = 43,

    /// <summary>
    /// The A key.
    /// </summary>
    A = 44,

    /// <summary>
    /// The B key.
    /// </summary>
    B = 45,

    /// <summary>
    /// The C key.
    /// </summary>
    C = 46,

    /// <summary>
    /// The D key.
    /// </summary>
    D = 47,

    /// <summary>
    /// The E key.
    /// </summary>
    E = 48,

    /// <summary>
    /// The F key.
    /// </summary>
    F = 49,

    /// <summary>
    /// The G key.
    /// </summary>
    G = 50,

    /// <summary>
    /// The H key.
    /// </summary>
    H = 51,

    /// <summary>
    /// The I key.
    /// </summary>
    I = 52,

    /// <summary>
    /// The J key.
    /// </summary>
    J = 53,

    /// <summary>
    /// The K key.
    /// </summary>
    K = 54,

    /// <summary>
    /// The L key.
    /// </summary>
    L = 55,

    /// <summary>
    /// The M key.
    /// </summary>
    M = 56,

    /// <summary>
    /// The N key.
    /// </summary>
    N = 57,

    /// <summary>
    /// The O key.
    /// </summary>
    O = 58,

    /// <summary>
    /// The P key.
    /// </summary>
    P = 59,

    /// <summary>
    /// The Q key.
    /// </summary>
    Q = 60,

    /// <summary>
    /// The R key.
    /// </summary>
    R = 61,

    /// <summary>
    /// The S key.
    /// </summary>
    S = 62,

    /// <summary>
    /// The T key.
    /// </summary>
    T = 63,

    /// <summary>
    /// The U key.
    /// </summary>
    U = 64,

    /// <summary>
    /// The V key.
    /// </summary>
    V = 65,

    /// <summary>
    /// The W key.
    /// </summary>
    W = 66,

    /// <summary>
    /// The X key.
    /// </summary>
    X = 67,

    /// <summary>
    /// The Y key.
    /// </summary>
    Y = 68,

    /// <summary>
    /// The Z key.
    /// </summary>
    Z = 69,

    /// <summary>
    /// The left Windows key.
    /// </summary>
    LWin = 70,

    /// <summary>
    /// The right Windows key.
    /// </summary>
    RWin = 71,

    /// <summary>
    /// The application menu key.
    /// </summary>
    Apps = 72,

    /// <summary>
    /// The Sleep key.
    /// </summary>
    Sleep = 73,

    /// <summary>
    /// The 0 key on the numeric keypad.
    /// </summary>
    NumPad0 = 74,

    /// <summary>
    /// The 1 key on the numeric keypad.
    /// </summary>
    NumPad1 = 75,

    /// <summary>
    /// The 2 key on the numeric keypad.
    /// </summary>
    NumPad2 = 76,

    /// <summary>
    /// The 3 key on the numeric keypad.
    /// </summary>
    NumPad3 = 77,

    /// <summary>
    /// The 4 key on the numeric keypad.
    /// </summary>
    NumPad4 = 78,

    /// <summary>
    /// The 5 key on the numeric keypad.
    /// </summary>
    NumPad5 = 79,

    /// <summary>
    /// The 6 key on the numeric keypad.
    /// </summary>
    NumPad6 = 80,

    /// <summary>
    /// The 7 key on the numeric keypad.
    /// </summary>
    NumPad7 = 81,

    /// <summary>
    /// The 8 key on the numeric keypad.
    /// </summary>
    NumPad8 = 82,

    /// <summary>
    /// The 9 key on the numeric keypad.
    /// </summary>
    NumPad9 = 83,

    /// <summary>
    /// The numeric keypad multiplication key.
    /// </summary>
    Multiply = 84,

    /// <summary>
    /// The numeric keypad addition key.
    /// </summary>
    Add = 85,

    /// <summary>
    /// The numeric keypad separator key.
    /// </summary>
    Separator = 86,

    /// <summary>
    /// The numeric keypad subtraction key.
    /// </summary>
    Subtract = 87,

    /// <summary>
    /// The numeric keypad decimal key.
    /// </summary>
    Decimal = 88,

    /// <summary>
    /// The numeric keypad division key.
    /// </summary>
    Divide = 89,

    /// <summary>
    /// The F1 function key.
    /// </summary>
    F1 = 90,

    /// <summary>
    /// The F2 function key.
    /// </summary>
    F2 = 91,

    /// <summary>
    /// The F3 function key.
    /// </summary>
    F3 = 92,

    /// <summary>
    /// The F4 function key.
    /// </summary>
    F4 = 93,

    /// <summary>
    /// The F5 function key.
    /// </summary>
    F5 = 94,

    /// <summary>
    /// The F6 function key.
    /// </summary>
    F6 = 95,

    /// <summary>
    /// The F7 function key.
    /// </summary>
    F7 = 96,

    /// <summary>
    /// The F8 function key.
    /// </summary>
    F8 = 97,

    /// <summary>
    /// The F9 function key.
    /// </summary>
    F9 = 98,

    /// <summary>
    /// The F10 function key.
    /// </summary>
    F10 = 99,

    /// <summary>
    /// The F11 function key.
    /// </summary>
    F11 = 100,

    /// <summary>
    /// The F12 function key.
    /// </summary>
    F12 = 101,

    /// <summary>
    /// The F13 function key.
    /// </summary>
    F13 = 102,

    /// <summary>
    /// The F14 function key.
    /// </summary>
    F14 = 103,

    /// <summary>
    /// The F15 function key.
    /// </summary>
    F15 = 104,

    /// <summary>
    /// The F16 function key.
    /// </summary>
    F16 = 105,

    /// <summary>
    /// The F17 function key.
    /// </summary>
    F17 = 106,

    /// <summary>
    /// The F18 function key.
    /// </summary>
    F18 = 107,

    /// <summary>
    /// The F19 function key.
    /// </summary>
    F19 = 108,

    /// <summary>
    /// The F20 function key.
    /// </summary>
    F20 = 109,

    /// <summary>
    /// The F21 function key.
    /// </summary>
    F21 = 110,

    /// <summary>
    /// The F22 function key.
    /// </summary>
    F22 = 111,

    /// <summary>
    /// The F23 function key.
    /// </summary>
    F23 = 112,

    /// <summary>
    /// The F24 function key.
    /// </summary>
    F24 = 113,

    /// <summary>
    /// The Num Lock key.
    /// </summary>
    NumLock = 114,

    /// <summary>
    /// The Scroll Lock key.
    /// </summary>
    Scroll = 115,

    /// <summary>
    /// The left Shift key.
    /// </summary>
    LeftShift = 116,

    /// <summary>
    /// The right Shift key.
    /// </summary>
    RightShift = 117,

    /// <summary>
    /// The left Control key.
    /// </summary>
    LeftCtrl = 118,

    /// <summary>
    /// The right Control key.
    /// </summary>
    RightCtrl = 119,

    /// <summary>
    /// The left Alt key.
    /// </summary>
    LeftAlt = 120,

    /// <summary>
    /// The right Alt key.
    /// </summary>
    RightAlt = 121,

    /// <summary>
    /// The browser Back key.
    /// </summary>
    BrowserBack = 122,

    /// <summary>
    /// The browser Forward key.
    /// </summary>
    BrowserForward = 123,

    /// <summary>
    /// The browser Refresh key.
    /// </summary>
    BrowserRefresh = 124,

    /// <summary>
    /// The browser Stop key.
    /// </summary>
    BrowserStop = 125,

    /// <summary>
    /// The browser Search key.
    /// </summary>
    BrowserSearch = 126,

    /// <summary>
    /// The browser Favorites key.
    /// </summary>
    BrowserFavorites = 127,

    /// <summary>
    /// The browser Home key.
    /// </summary>
    BrowserHome = 128,

    /// <summary>
    /// The volume mute key.
    /// </summary>
    VolumeMute = 129,

    /// <summary>
    /// The volume decrease key.
    /// </summary>
    VolumeDown = 130,

    /// <summary>
    /// The volume increase key.
    /// </summary>
    VolumeUp = 131,

    /// <summary>
    /// The next media track key.
    /// </summary>
    MediaNextTrack = 132,

    /// <summary>
    /// The previous media track key.
    /// </summary>
    MediaPreviousTrack = 133,

    /// <summary>
    /// The media Stop key.
    /// </summary>
    MediaStop = 134,

    /// <summary>
    /// The media Play/Pause key.
    /// </summary>
    MediaPlayPause = 135,

    /// <summary>
    /// The mail application key.
    /// </summary>
    LaunchMail = 136,

    /// <summary>
    /// The media selection key.
    /// </summary>
    SelectMedia = 137,

    /// <summary>
    /// The first application launch key.
    /// </summary>
    LaunchApplication1 = 138,

    /// <summary>
    /// The second application launch key.
    /// </summary>
    LaunchApplication2 = 139,

    /// <summary>
    /// The first keyboard-layout-specific key, commonly semicolon.
    /// </summary>
    Oem1 = 140,

    /// <summary>
    /// The keyboard-layout-specific semicolon key; an alias for Oem1.
    /// </summary>
    OemSemicolon = 140,

    /// <summary>
    /// The keyboard-layout-specific plus key.
    /// </summary>
    OemPlus = 141,

    /// <summary>
    /// The keyboard-layout-specific comma key.
    /// </summary>
    OemComma = 142,

    /// <summary>
    /// The keyboard-layout-specific minus key.
    /// </summary>
    OemMinus = 143,

    /// <summary>
    /// The keyboard-layout-specific period key.
    /// </summary>
    OemPeriod = 144,

    /// <summary>
    /// The second keyboard-layout-specific key, commonly slash and question mark.
    /// </summary>
    Oem2 = 145,

    /// <summary>
    /// The keyboard-layout-specific question mark key; an alias for Oem2.
    /// </summary>
    OemQuestion = 145,

    /// <summary>
    /// The third keyboard-layout-specific key, commonly grave accent and tilde.
    /// </summary>
    Oem3 = 146,

    /// <summary>
    /// The keyboard-layout-specific tilde key; an alias for Oem3.
    /// </summary>
    OemTilde = 146,

    /// <summary>
    /// The first Brazilian keyboard-specific key.
    /// </summary>
    AbntC1 = 147,

    /// <summary>
    /// The second Brazilian keyboard-specific key.
    /// </summary>
    AbntC2 = 148,

    /// <summary>
    /// The fourth keyboard-layout-specific key, commonly opening bracket.
    /// </summary>
    Oem4 = 149,

    /// <summary>
    /// The keyboard-layout-specific opening bracket key; an alias for Oem4.
    /// </summary>
    OemOpenBrackets = 149,

    /// <summary>
    /// The fifth keyboard-layout-specific key, commonly backslash and vertical bar.
    /// </summary>
    Oem5 = 150,

    /// <summary>
    /// The keyboard-layout-specific vertical bar key; an alias for Oem5.
    /// </summary>
    OemPipe = 150,

    /// <summary>
    /// The sixth keyboard-layout-specific key, commonly closing bracket.
    /// </summary>
    Oem6 = 151,

    /// <summary>
    /// The keyboard-layout-specific closing bracket key; an alias for Oem6.
    /// </summary>
    OemCloseBrackets = 151,

    /// <summary>
    /// The seventh keyboard-layout-specific key, commonly quotation marks.
    /// </summary>
    Oem7 = 152,

    /// <summary>
    /// The keyboard-layout-specific quotation mark key; an alias for Oem7.
    /// </summary>
    OemQuotes = 152,

    /// <summary>
    /// The eighth keyboard-layout-specific key.
    /// </summary>
    Oem8 = 153,

    /// <summary>
    /// The additional keyboard-layout-specific key found on some keyboards.
    /// </summary>
    Oem102 = 154,

    /// <summary>
    /// The additional keyboard-layout-specific backslash key; an alias for Oem102.
    /// </summary>
    OemBackslash = 154,

    /// <summary>
    /// A key processed by an input method.
    /// </summary>
    ImeProcessed = 155,

    /// <summary>
    /// A key processed as a system key, such as one pressed with Alt.
    /// </summary>
    System = 156,

    /// <summary>
    /// The alphanumeric input-mode key.
    /// </summary>
    DbeAlphanumeric = 157,

    /// <summary>
    /// The keyboard-specific attention key; shares its value with DbeAlphanumeric.
    /// </summary>
    OemAttn = 157,

    /// <summary>
    /// The Katakana input-mode key.
    /// </summary>
    DbeKatakana = 158,

    /// <summary>
    /// The keyboard-specific Finish key; shares its value with DbeKatakana.
    /// </summary>
    OemFinish = 158,

    /// <summary>
    /// The Hiragana input-mode key.
    /// </summary>
    DbeHiragana = 159,

    /// <summary>
    /// The keyboard-specific Copy key; shares its value with DbeHiragana.
    /// </summary>
    OemCopy = 159,

    /// <summary>
    /// The single-byte character input-mode key.
    /// </summary>
    DbeSbcsChar = 160,

    /// <summary>
    /// The keyboard-specific Auto key; shares its value with DbeSbcsChar.
    /// </summary>
    OemAuto = 160,

    /// <summary>
    /// The double-byte character input-mode key.
    /// </summary>
    DbeDbcsChar = 161,

    /// <summary>
    /// The keyboard-specific Enlarge Window key; shares its value with DbeDbcsChar.
    /// </summary>
    OemEnlw = 161,

    /// <summary>
    /// The Roman character input-mode key.
    /// </summary>
    DbeRoman = 162,

    /// <summary>
    /// The keyboard-specific Back Tab key; shares its value with DbeRoman.
    /// </summary>
    OemBackTab = 162,

    /// <summary>
    /// The Attention key.
    /// </summary>
    Attn = 163,

    /// <summary>
    /// The non-Roman character input-mode key; shares its value with Attn.
    /// </summary>
    DbeNoRoman = 163,

    /// <summary>
    /// The Cursor Select key.
    /// </summary>
    CrSel = 164,

    /// <summary>
    /// The word registration input-mode key; shares its value with CrSel.
    /// </summary>
    DbeEnterWordRegisterMode = 164,

    /// <summary>
    /// The input method configuration key.
    /// </summary>
    DbeEnterImeConfigureMode = 165,

    /// <summary>
    /// The Extend Selection key; shares its value with DbeEnterImeConfigureMode.
    /// </summary>
    ExSel = 165,

    /// <summary>
    /// The input method key that accepts the current text.
    /// </summary>
    DbeFlushString = 166,

    /// <summary>
    /// The Erase End of File key; shares its value with DbeFlushString.
    /// </summary>
    EraseEof = 166,

    /// <summary>
    /// The character-code input-mode key.
    /// </summary>
    DbeCodeInput = 167,

    /// <summary>
    /// The Play key; shares its value with DbeCodeInput.
    /// </summary>
    Play = 167,

    /// <summary>
    /// The key that exits character-code input mode.
    /// </summary>
    DbeNoCodeInput = 168,

    /// <summary>
    /// The Zoom key; shares its value with DbeNoCodeInput.
    /// </summary>
    Zoom = 168,

    /// <summary>
    /// The input method key that determines the current text.
    /// </summary>
    DbeDetermineString = 169,

    /// <summary>
    /// The unnamed key; shares its value with DbeDetermineString.
    /// </summary>
    NoName = 169,

    /// <summary>
    /// The dialog conversion input-mode key.
    /// </summary>
    DbeEnterDialogConversionMode = 170,

    /// <summary>
    /// The PA1 key; shares its value with DbeEnterDialogConversionMode.
    /// </summary>
    Pa1 = 170,

    /// <summary>
    /// The keyboard-specific Clear key.
    /// </summary>
    OemClear = 171,

    /// <summary>
    /// A key processed as a character that combines with a later character.
    /// </summary>
    DeadCharProcessed = 172
}
