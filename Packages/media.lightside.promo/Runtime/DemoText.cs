namespace LightSide.Promo
{
    /// <summary>
    /// The strings the film renders. One place, because several shots must show the <em>same</em> text and a
    /// comparison whose two sides drift apart proves nothing.
    /// </summary>
    /// <remarks>
    /// <see cref="Postcard"/> is deliberately hostile. Every line mixes a right-to-left script with Latin words,
    /// Western digits, punctuation and emoji, which is where engines fail: a phone number inside Arabic exercises
    /// bidirectional resolution, a price inside Thai exercises it again, a quoted Latin phrase inside Arabic
    /// exercises the mirrored bracket pair, and the family sequence exercises zero-width joiners. It is not a
    /// showcase sentence chosen to flatter — it is ordinary holiday chatter, which is the point.
    /// </remarks>
    public static class DemoText
    {
        public const string Postcard =
            "Hey! 🎉 Just got back from vacation.\n" +
            "\n" +
            "زرت Paris و London خلال 5 أيام (رحلة رائعة!) 🌍\n" +
            "اتصل بي على +972-50-123-4567 الساعة 8 PM 📞\n" +
            "قال Tom: \"Hello world\" وضحك كثيراً 😄\n" +
            "\n" +
            "देखी \"हिन्दी सिनेमा\" festival — incredible! 🎬\n" +
            "อาหารไทยอร่อยมาก! ราคา ฿250 per meal 🍜\n" +
            "My family 👨‍👩‍👧‍👦 loved it. Pizza after? 🍕";

        /// <summary>The Arabic line carrying a phone number: bidirectional resolution at its hardest.</summary>
        public const string Phone = "اتصل بي على +972-50-123-4567 الساعة 8 PM 📞";

        /// <summary>Arabic around a quoted Latin phrase, which also mirrors the bracket pair.</summary>
        public const string Quoted = "قال Tom: \"Hello world\" وضحك كثيراً 😄";

        /// <summary>Thai with no spaces between words, plus a currency symbol and a price.</summary>
        public const string Thai = "อาหารไทยอร่อยมาก! ราคา ฿250 per meal 🍜";

        /// <summary>Devanagari conjuncts and matras around a Latin word.</summary>
        public const string Hindi = "देखी \"हिन्दी सिनेमा\" festival — incredible! 🎬";

        /// <summary>A four-person family: seven codepoints joined into one glyph.</summary>
        public const string Family = "My family 👨‍👩‍👧‍👦 loved it. Pizza after? 🍕";

        /// <summary>One word that must join, and whose lam-alef pair is a mandatory ligature.</summary>
        public const string Students = "طلاب";

        /// <summary>Scripts for the wall, one line each.</summary>
        public static readonly string[] Wall =
        {
            "مرحبا بالعالم",
            "שלום עולם",
            "नमस्ते दुनिया",
            "สวัสดีชาวโลก",
            "こんにちは世界",
            "გამარჯობა მსოფლიო",
            "Բարեւ աշխարհ",
            "안녕하세요 세계"
        };
    }
}
