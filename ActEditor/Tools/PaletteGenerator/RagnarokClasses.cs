using System;
using System.Collections.Generic;
using System.Linq;

namespace ActEditor.Tools.PaletteGenerator {
	/// <summary>
	/// Provides a static list of all Ragnarok Online character classes with their proper naming format
	/// </summary>
	public static class RagnarokClasses {
		public class ClassInfo {
			public string Code { get; set; }
			public string DisplayName { get; set; }
			public string Category { get; set; }

			public ClassInfo(string code, string displayName, string category) {
				Code = code;
				DisplayName = displayName;
				Category = category;
			}

			public override string ToString() {
				return String.IsNullOrEmpty(DisplayName) ? Code : String.Format("{0} ({1})", DisplayName, Code);
			}
		}

		private static readonly List<ClassInfo> _allClasses = new List<ClassInfo> {
			// Novices
			new ClassInfo("ÃÊº¸ÀÚ", "Novice", "Novices"),
			new ClassInfo("½´ÆÛ³ëºñ½º", "Super Novice / Expanded Super Novice", "Novices"),

			// 1st Jobs
			new ClassInfo("¼ºÁ÷ÀÚ", "Acolyte", "1st Jobs"),
			new ClassInfo("¼ºÁ÷ÀÚ_h", "Acolyte", "1st Jobs"),
			new ClassInfo("±Ã¼ö", "Archer", "1st Jobs"),
			new ClassInfo("¸¶¹ý»ç", "Magician", "1st Jobs"),
			new ClassInfo("»óÀÎ", "Merchant", "1st Jobs"),
			new ClassInfo("°Ë»ç", "Swordsman", "1st Jobs"),
			new ClassInfo("µµµÏ", "Thief", "1st Jobs"),

			// 2-1 Jobs
			new ClassInfo("ÇÁ¸®½ºÆ®", "Priest", "2-1 Jobs"),
			new ClassInfo("ÇÁ¸®½ºÆ®_h", "Priest", "2-1 Jobs"),
			new ClassInfo("¼ºÅõ»ç", "Priest", "2-1 Jobs"),
			new ClassInfo("ÇåÅÍ", "Hunter", "2-1 Jobs"),
			new ClassInfo("ÇåÅÍ_h", "Hunter", "2-1 Jobs"),
			new ClassInfo("À§Àúµå", "Wizard", "2-1 Jobs"),
			new ClassInfo("À§Àúµå_h", "Wizard", "2-1 Jobs"),
			new ClassInfo("Á¦Ã¶°ø", "Blacksmith", "2-1 Jobs"),
			new ClassInfo("Á¦Ã¶°ø_h", "Blacksmith", "2-1 Jobs"),
			new ClassInfo("±â»ç", "Knight", "2-1 Jobs"),
			new ClassInfo("±â»ç_h", "Knight", "2-1 Jobs"),
			new ClassInfo("¾î¼¼½Å", "Assassin", "2-1 Jobs"),
			new ClassInfo("¾î¼¼½Å_h", "Assassin", "2-1 Jobs"),

			// 2-2 Jobs
			new ClassInfo("¸ùÅ©", "Monk", "2-2 Jobs"),
			new ClassInfo("¸ùÅ©_h", "Monk", "2-2 Jobs"),
			new ClassInfo("¹Ùµå", "Bard", "2-2 Jobs"),
			new ClassInfo("¹Ùµå_h", "Bard", "2-2 Jobs"),
			new ClassInfo("¹«Èñ", "Dancer", "2-2 Jobs"),
			new ClassInfo("¹«Èñ_h", "Dancer", "2-2 Jobs"),
			new ClassInfo("¼¼ÀÌÁö", "Sage", "2-2 Jobs"),
			new ClassInfo("¼¼ÀÌÁö_h", "Sage", "2-2 Jobs"),
			new ClassInfo("¿¬±Ý¼ú»ç", "Alchemist", "2-2 Jobs"),
			new ClassInfo("¿¬±Ý¼ú»ç_h", "Alchemist", "2-2 Jobs"),
			new ClassInfo("Å©·ç¼¼ÀÌ´õ", "Crusader", "2-2 Jobs"),
			new ClassInfo("Å©·ç¼¼ÀÌ´õ_h", "Crusader", "2-2 Jobs"),
			new ClassInfo("·Î±×", "Rogue", "2-2 Jobs"),
			new ClassInfo("·Î±×_h", "Rogue", "2-2 Jobs"),

			// Transcendent 2-1 Jobs
			new ClassInfo("ÇÏÀÌÇÁ¸®", "High Priest", "Transcendent 2-1 Jobs"),
			new ClassInfo("¼ºÅõ»ç2", "High Priest", "Transcendent 2-1 Jobs"),
			new ClassInfo("½º³ªÀÌÆÛ", "Sniper", "Transcendent 2-1 Jobs"),
			new ClassInfo("ÇÏÀÌÀ§Àúµå", "High Wizard", "Transcendent 2-1 Jobs"),
			new ClassInfo("È­ÀÌÆ®½º¹Ì½º", "Whitesmith", "Transcendent 2-1 Jobs"),
			new ClassInfo("·Îµå³ªÀÌÆ®", "Lord Knight", "Transcendent 2-1 Jobs"),
			new ClassInfo("¾î½Ø½ÅÅ©·Î½º", "Assassin Cross", "Transcendent 2-1 Jobs"),

			// Transcendent 2-2 Jobs
			new ClassInfo("Ã¨ÇÇ¿Â", "Champion", "Transcendent 2-2 Jobs"),
			new ClassInfo("Å¬¶ó¿î", "Clown", "Transcendent 2-2 Jobs"),
			new ClassInfo("Áý½Ã", "Gypsy", "Transcendent 2-2 Jobs"),
			new ClassInfo("ÇÁ·ÎÆä¼­", "Professor", "Transcendent 2-2 Jobs"),
			new ClassInfo("Å©¸®¿¡ÀÌÅÍ", "Creator", "Transcendent 2-2 Jobs"),
			new ClassInfo("ÆÈ¶óµò", "Paladin", "Transcendent 2-2 Jobs"),
			new ClassInfo("½ºÅäÄ¿", "Stalker", "Transcendent 2-2 Jobs"),

			// 3-1 Jobs
			new ClassInfo("¾ÆÅ©ºñ¼ó", "Archbishop", "3-1 Jobs"),
			new ClassInfo("·¹ÀÎÁ®", "Ranger", "3-1 Jobs"),
			new ClassInfo("¿ö·Ï", "Warlock", "3-1 Jobs"),
			new ClassInfo("¹ÌÄÉ´Ð", "Mechanic", "3-1 Jobs"),
			new ClassInfo("·é³ªÀÌÆ®", "Rune Knight", "3-1 Jobs"),
			new ClassInfo("±æ·ÎÆ¾Å©·Î½º", "Guillotine Cross", "3-1 Jobs"),

			// 3-2 Jobs
			new ClassInfo("½´¶ó", "Sura", "3-2 Jobs"),
			new ClassInfo("¹Î½ºÆ®·²", "Minstrel", "3-2 Jobs"),
			new ClassInfo("¿ø´õ·¯", "Wanderer", "3-2 Jobs"),
			new ClassInfo("¼Ò¼­·¯", "Sorcerer", "3-2 Jobs"),
			new ClassInfo("Á¦³×¸¯", "Genetic", "3-2 Jobs"),
			new ClassInfo("°¡µå", "Royal Guard", "3-2 Jobs"),
			new ClassInfo("½¦µµ¿ìÃ¼ÀÌ¼­", "Shadow Chaser", "3-2 Jobs"),

			// Expanded Jobs
			new ClassInfo("°Ç³Ê", "Gunslinger", "Expanded Jobs"),
			new ClassInfo("´ÑÀÚ", "Ninja", "Expanded Jobs"),
			new ClassInfo("ÅÂ±Ç¼Ò³â", "Taekwon", "Expanded Jobs"),
			new ClassInfo("±Ç¼º", "Star Gladiator", "Expanded Jobs"),
			new ClassInfo("¼Ò¿ï¸µÄ¿", "Soul Linker", "Expanded Jobs"),
			new ClassInfo("¼ºÁ¦", "Star Emperor", "Expanded Jobs"),
			new ClassInfo("¼Ò¿ï¸®ÆÛ", "Soul Reaper", "Expanded Jobs"),

			// Cash Mounts - Novices
			new ClassInfo("³ëºñ½ºÆ÷¸µ", "Poring Novice", "Cash Mounts"),
			new ClassInfo("½´ÆÛ³ëºñ½ºÆ÷¸µ", "Poring Super Novice / Expanded Super Novice", "Cash Mounts"),

			// Cash Mounts - 1st
			new ClassInfo("º¹»ç¾ËÆÄÄ«", "Alpaca Acolyte", "Cash Mounts"),
			new ClassInfo("Å¸Á¶±Ã¼ö", "Ostrich Archer", "Cash Mounts"),
			new ClassInfo("¿©¿ì¸¶¹ý»ç", "Nine Tail Magician", "Cash Mounts"),
			new ClassInfo("»óÀÎ¸äµÅÁö", "Savage Merchant", "Cash Mounts"),
			new ClassInfo("ÆäÄÚ°Ë»ç", "Peco Peco Swordsman", "Cash Mounts"),
			new ClassInfo("ÄÌº£·Î½ºµµµÏ", "Galleon Thief", "Cash Mounts"),

			// Cash Mounts - 2-1
			new ClassInfo("ÇÁ¸®½ºÆ®¾ËÆÄÄ«", "Alpaca Priest", "Cash Mounts"),
			new ClassInfo("Å¸Á¶ÇåÅÍ", "Ostrich Hunter", "Cash Mounts"),
			new ClassInfo("¿©¿ìÀ§Àúµå", "Nine Tail Wizard", "Cash Mounts"),
			new ClassInfo("Á¦Ã¶°ø¸äµÅÁö", "Savage Blacksmith", "Cash Mounts"),
			new ClassInfo("»çÀÚ±â»ç", "King Lion Knight", "Cash Mounts"),
			new ClassInfo("ÄÌº£·Î½º¾î½ê½Å", "Galleon Assassin", "Cash Mounts"),

			// Cash Mounts - 2-2
			new ClassInfo("¸ùÅ©¾ËÆÄÄ«", "Alpaca Monk", "Cash Mounts"),
			new ClassInfo("Å¸Á¶¹Ùµå", "Ostrich Bard", "Cash Mounts"),
			new ClassInfo("Å¸Á¶¹«Èñ", "Ostrich Dancer", "Cash Mounts"),
			new ClassInfo("¿©¿ì¼¼ÀÌÁö", "Nine Tail Sage", "Cash Mounts"),
			new ClassInfo("¿¬±Ý¼ú»ç¸äµÅÁö", "Savage Alchemist", "Cash Mounts"),
			new ClassInfo("»çÀÚÅ©·ç¼¼ÀÌ´õ", "King Lion Crusader", "Cash Mounts"),
			new ClassInfo("ÄÌº£·Î½º·Î±×", "Galleon Rogue", "Cash Mounts"),

			// Cash Mounts - T2-1
			new ClassInfo("ÇÏÀÌÇÁ¸®½ºÆ®¾ËÆÄÄ«", "Alpaca High Priest", "Cash Mounts"),
			new ClassInfo("Å¸Á¶½º³ªÀÌÆÛ", "Ostrich Sniper", "Cash Mounts"),
			new ClassInfo("¿©¿ìÇÏÀÌÀ§Àúµå", "Nine Tail High Wizard", "Cash Mounts"),
			new ClassInfo("È­ÀÌÆ®½º¹Ì½º¸äµÅÁö", "Savage Whitesmith", "Cash Mounts"),
			new ClassInfo("»çÀÚ·Îµå³ªÀÌÆ®", "King Lion Lord Knight", "Cash Mounts"),
			new ClassInfo("ÄÌº£·Î½º¾î½ê½ÅÅ©·Î½º", "Galleon Assassin Cross", "Cash Mounts"),

			// Cash Mounts - T2-2
			new ClassInfo("Ã¨ÇÇ¿Â¾ËÆÄÄ«", "Alpaca Champion", "Cash Mounts"),
			new ClassInfo("Å¸Á¶Å©¶ó¿î", "Ostrich Clown", "Cash Mounts"),
			new ClassInfo("Å¸Á¶Â¤½Ã", "Ostrich Gypsy", "Cash Mounts"),
			new ClassInfo("¿©¿ìÇÁ·ÎÆä¼­", "Nine Tail Professor", "Cash Mounts"),
			new ClassInfo("Å©¸®¿¡ÀÌÅÍ¸äµÅÁö", "Savage Creator", "Cash Mounts"),
			new ClassInfo("»çÀÚÆÈ¶óµò", "King Lion Paladin", "Cash Mounts"),
			new ClassInfo("ÄÌº£·Î½º½ºÅäÄ¿", "Galleon Stalker", "Cash Mounts"),

			// Cash Mounts - 3-1
			new ClassInfo("¾ÆÅ©ºñ¼ó¾ËÆÄÄ«", "Alpaca Archbishop", "Cash Mounts"),
			new ClassInfo("Å¸Á¶·¹ÀÎÁ®", "Ostrich Ranger", "Cash Mounts"),
			new ClassInfo("¿©¿ì¿ö·Ï", "Nine Tail Warlock", "Cash Mounts"),
			new ClassInfo("¹ÌÄÉ´Ð¸äµÅÁö", "Savage Mechanic", "Cash Mounts"),
			new ClassInfo("»çÀÚ·é³ªÀÌÆ®", "King Lion Rune Knight", "Cash Mounts"),
			new ClassInfo("ÄÌº£·Î½º±æ·ÎÆ¾Å©·Î½º", "Galleon Guillotine Cross", "Cash Mounts"),

			// Cash Mounts - 3-2
			new ClassInfo("½´¶ó¾ËÆÄÄ«", "Alpaca Sura", "Cash Mounts"),
			new ClassInfo("Å¸Á¶¹Î½ºÆ®·²", "Ostrich Minstrel", "Cash Mounts"),
			new ClassInfo("Å¸Á¶¿ø´õ·¯", "Ostrich Wanderer", "Cash Mounts"),
			new ClassInfo("¿©¿ì¼Ò¼­·¯", "Nine Tail Sorcerer", "Cash Mounts"),
			new ClassInfo("Á¦³×¸¯¸äµÅÁö", "Savage Genetic", "Cash Mounts"),
			new ClassInfo("»çÀÚ·Î¾â°¡µå", "King Lion Royal Guard", "Cash Mounts"),
			new ClassInfo("ÄÌº£·Î½º½¦µµ¿ìÃ¼ÀÌ¼­", "Galleon Shadow Chaser", "Cash Mounts"),

			// Expanded - Cash Mounts
			new ClassInfo("ÅÂ±Ç¼Ò³âÆ÷¸µ", "Poring Taekwon", "Cash Mounts"),
			new ClassInfo("µÎ²¨ºñ´ÑÀÚ", "Poison Toad Ninja", "Cash Mounts"),
			new ClassInfo("ÆäÄÚ°Ç³Ê", "Bike Gunslinger (Peco Peco in older version)", "Cash Mounts"),
			new ClassInfo("±Ç¼ºÆ÷¸µ", "Poring Star Gladiator", "Cash Mounts"),
			new ClassInfo("µÎ²¨ºñ¼Ò¿ï¸µÄ¿", "Poison Toad Soul Linker", "Cash Mounts"),
			new ClassInfo("ÇØÅÂ¼ºÁ¦", "Haetae Star Emperor", "Cash Mounts"),
			new ClassInfo("ÇØÅÂ¼Ò¿ï¸®ÆÛ", "Haetae Soul Reaper", "Cash Mounts"),

			// Skill/Combat Mounts
			new ClassInfo("ÆäÄÚÆäÄÚ_±â»ç", "Peco Peco Knight", "Skill/Combat Mounts"),
			new ClassInfo("ÆäÄÚÆäÄÚ_±â»ç_h", "Peco Peco Knight", "Skill/Combat Mounts"),
			new ClassInfo("·ÎµåÆäÄÚ", "Armored Peco Peco Lord Knight", "Skill/Combat Mounts"),
			new ClassInfo("·é³ªÀÌÆ®»Ú¶ì", "Ferus Rune Knight", "Skill/Combat Mounts"),
			new ClassInfo("·é³ªÀÌÆ®»Ú¶ì2", "Black Ferus Rune Knight", "Skill/Combat Mounts"),
			new ClassInfo("·é³ªÀÌÆ®»Ú¶ì3", "White Ferus Rune Knight", "Skill/Combat Mounts"),
			new ClassInfo("·é³ªÀÌÆ®»Ú¶ì4", "Blue Ferus Rune Knight", "Skill/Combat Mounts"),
			new ClassInfo("·é³ªÀÌÆ®»Ú¶ì5", "Red Ferus Rune Knight", "Skill/Combat Mounts"),
			new ClassInfo("·¹ÀÎÁ®´Á´ë", "Warg Ranger", "Skill/Combat Mounts"),
			new ClassInfo("¸¶µµ±â¾î", "Magic Gear Mechanic", "Skill/Combat Mounts"),
			new ClassInfo("¸¶µµ¾Æ¸Ó", "Magic Gear Mechanic (jRO)", "Skill/Combat Mounts"),
			new ClassInfo("±¸ÆäÄÚÅ©·ç¼¼ÀÌ´õ", "Peco Peco Crusader", "Skill/Combat Mounts"),
			new ClassInfo("½ÅÆäÄÚÅ©·ç¼¼ÀÌ´õ", "Grand Peco Crusader", "Skill/Combat Mounts"),
			new ClassInfo("½ÅÆäÄÚÅ©·ç¼¼ÀÌ´õ_h", "Grand Peco Crusader", "Skill/Combat Mounts"),
			new ClassInfo("ÆäÄÚÆÈ¶óµò", "Armored Grand Peco Paladin", "Skill/Combat Mounts"),
			new ClassInfo("±×¸®Æù°¡µå", "Gryphon Royal Guard", "Skill/Combat Mounts"),

			// Others
			new ClassInfo("¹«Èñ_¿©_¹ÙÁö", "Pants Dancer", "Others"),
			new ClassInfo("±Ç¼ºÀ¶ÇÕ", "Floating Star Gladiator", "Others"),
			new ClassInfo("¼ºÁ¦À¶ÇÕ", "Floating Star Emperor", "Others"),

			// Costumes
			new ClassInfo("»êÅ¸", "Christmas", "Costumes"),
			new ClassInfo("¿©¸§", "Summer", "Costumes"),
			new ClassInfo("¿©¸§2", "Summer 2", "Costumes"),
			new ClassInfo("°áÈ¥", "Wedding", "Costumes"),
			new ClassInfo("ÅÎ½Ãµµ", "Wedding", "Costumes"),
			new ClassInfo("ÇÑº¹", "Hanbok", "Costumes"),
			new ClassInfo("¿ÁÅä¹öÆÐ½ºÆ®", "Oktoberfest", "Costumes"),

			// GMs
			new ClassInfo("¿î¿µÀÚ", "Gamemaster", "GMs"),
			new ClassInfo("¿î¿µÀÚ2", "Gamemaster 2", "GMs"),

			// Mercenaries
			new ClassInfo("°Ë¿ëº´", "Mercenary Fencer (Male)", "Mercenaries"),
			new ClassInfo("Ã¢¿ëº´", "Mercenary Spearman (Male)", "Mercenaries"),
			new ClassInfo("È°¿ëº´", "Mercenary Bowman (Female)", "Mercenaries")
		};

		/// <summary>
		/// Gets all classes
		/// </summary>
		public static List<ClassInfo> GetAllClasses() {
			return _allClasses.ToList();
		}

		/// <summary>
		/// Gets classes grouped by category
		/// </summary>
		public static Dictionary<string, List<ClassInfo>> GetClassesByCategory() {
			return _allClasses.GroupBy(c => c.Category).ToDictionary(g => g.Key, g => g.ToList());
		}

		/// <summary>
		/// Finds a class by its code
		/// </summary>
		public static ClassInfo FindByCode(string code) {
			return _allClasses.FirstOrDefault(c => c.Code == code);
		}
	}
}
