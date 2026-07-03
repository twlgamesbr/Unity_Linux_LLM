#!/usr/bin/env python3
"""
Mystery Scene Template Batch Generator
=======================================
Generates many variations of mystery scene templates from theme pools and
character role templates. Each variation is a valid JSON file matching the
MysterySceneTemplate serialization schema used by MysterySceneTemplateGenerator.cs.

Usage:
    # Generate 10 random variations (default)
    python generate_mystery_variations.py

    # Generate 25 variations with a specific seed
    python generate_mystery_variations.py --count 25 --seed 42

    # Generate variations for specific themes only
    python generate_mystery_variations.py --themes noir,victorian --count 5

    # Output to a custom directory
    python generate_mystery_variations.py --output Assets/MysteryTemplates/my_variations

    # Dry run: show what would be generated without writing files
    python generate_mystery_variations.py --count 3 --dry-run

    # List available themes and exit
    python generate_mystery_variations.py --list-themes
"""

import json
import os
import random
import re
import argparse
import sys
from pathlib import Path


# =============================================================================
# THEME POOLS
# =============================================================================

THEMES = {
    "victorian": {
        "name": "Victorian Manor",
        "setting_victim": ["Lord Ashworth", "Sir Reginald Croft", "Lady Penelope",
                           "The Honorable Mr. Whitmore", "Baroness Von Strauss"],
        "setting_location": ["Conservatory", "Library", "Drawing Room", "Ballroom",
                             "Study", "Wine Cellar", "Morning Room", "Gallery Hall"],
        "setting_desc": "a fog-shrouded Victorian country estate in autumn",
        "atmosphere": ["candlelit corridors", "creaking floorboards", "portraits with watchful eyes",
                       "fog pressing against the windows", "the distant howl of a hound"],
    },
    "noir": {
        "name": "Noir City",
        "setting_victim": ["Eddie 'The Shark' Marchetti", "Mona LeBlanc",
                           "Commissioner Hargrave", "Lola Del Rey", "Big Tony Scarpelli"],
        "setting_location": ["Dockside Warehouse", "Rooftop Lounge", "Back Alley Office",
                             "Speakeasy Basement", "Hotel Room 707", "Jazz Club Dressing Room",
                             "Pier 13", "Chinatown Apartment"],
        "setting_desc": "a rain-slicked city of crooked cops and femme fatales",
        "atmosphere": ["neon signs flickering in puddles", "cigarette smoke curling in the dark",
                       "a saxophone wailing from a nearby club", "tires hissing on wet asphalt",
                       "a single bare bulb swinging overhead"],
    },
    "sci_fi": {
        "name": "Starship Colony",
        "setting_victim": ["Captain Zhukova", "Dr. Aris Thorne", "Ambassador Vex",
                           "Chief Engineer Kovaks", "Pilot-Sage Mei-Lin"],
        "setting_location": ["Cryo Bay 4", "Observation Deck", "Medical Bay",
                             "Hydroponics Dome", "AI Core Chamber", "Airlock 7",
                             "Comm Relay Station", "Captain's Quarters"],
        "setting_desc": "a generational starship drifting through deep space",
        "atmosphere": ["the hum of life support systems", "flickering holographic displays",
                       "zero-gravity maintenance tunnels", "the distant thrum of the warp core",
                       "emergency lights casting long red shadows"],
    },
    "medieval": {
        "name": "Medieval Castle",
        "setting_victim": ["King Aldric", "Duke Geoffrey", "Lady Isolde",
                           "Baron Malcom the Stern", "Princess Elara"],
        "setting_location": ["Great Hall", "Tower Top Chamber", "Armory",
                             "Dungeon Cells", "Chapel Crypt", "Royal Bedchamber",
                             "Kitchen Hearth", "Stables Loft"],
        "setting_desc": "a stone fortress in a kingdom torn by intrigue",
        "atmosphere": ["torches sputtering in iron sconces", "the clank of armored guards",
                       "rain streaming down castle walls", "tapestries swaying in cold drafts",
                       "the smell of woodsmoke and tallow"],
    },
    "cozy_village": {
        "name": "Cozy English Village",
        "setting_victim": ["Mrs. Patricia Weatherby", "Colonel Fitzhugh",
                           "Vicar Archibald Moore", "Miss Agatha Primrose",
                           "Old Mr. Henderson the Baker"],
        "setting_location": ["Church Vestibule", "Village Green Gazebo",
                             "Tea Room Back Parlor", "Bookshop Basement",
                             "Manor House Study", "Pumpkin Patch Shed",
                             "Village Hall Stage", "Fishing Dock"],
        "setting_desc": "a quaint English village where everyone knows everyone",
        "atmosphere": ["church bells chiming the hour", "the crackle of a fireplace",
                       "steam rising from tea cups", "a cat weaving between table legs",
                       "the smell of freshly baked scones"],
    },
    "theatre": {
        "name": "West End Theatre",
        "setting_victim": ["Sir Laurence Croft", "Margot Fontaine",
                           "Director Desmond Pierce", "Prima Donna Celeste",
                           "Playwright Harold Finch"],
        "setting_location": ["Stage Trap Room", "Dressing Room A", "The Balcony",
                             "Prop Storage Basement", "Orchestra Pit",
                             "Green Room", "Box Seat 12", "Costume Wardrobe"],
        "setting_desc": "a grand theatre during opening night of a new play",
        "atmosphere": ["the murmur of an expectant audience", "houselights dimming",
                       "the smell of stage makeup and dust", "costumed actors hurrying through dim corridors",
                       "a single spotlight illuminating an empty stage"],
    },
}


# =============================================================================
# CHARACTER ROLE TEMPLATES (5 per mystery)
# =============================================================================
# Each role has:
#   - system_prompt: a system prompt template with placeholders
#   - knowledge_md: knowledge markdown template with placeholders
#   - is_culprit: boolean
#   - portrait_hint: hints at which existing portrait to map
#
# Placeholders (all upper-case in {{double braces}}):
#   {{VICTIM}}        - the victim's name
#   {{CULPRIT}}       - the culprit's display name
#   {{CRIME_LOCATION}} - where the crime happened
#   {{EVIDENCE_ITEM}}  - the key evidence item
#   {{SETTING}}       - setting description
#   {{ATMOSPHERE_1}}  - atmospheric detail
#   {{ATMOSPHERE_2}}  - another atmospheric detail
#   {{NPC_NAME}}      - this NPC's display name (filled by character role pool)
#   {{TIME_1}}, {{TIME_2}} - time references

CHARACTER_ROLES = {
    "suspicious_heir": {
        "label": "Suspicious Heir",
        "is_culprit": False,
        "system_prompt": (
            "You are {{NPC_NAME}}, the victim's closest relative and primary heir. "
            "You are genuinely grieving, but you have secrets about the will and your own financial troubles. "
            "You become defensive when pressed about your alibi. Answer only from your knowledge. "
            "If you do not know something, say so in character."
        ),
        "knowledge_md": (
            "# {{NPC_NAME}}'s knowledge\n\n"
            "## Background\n\n"
            "I am {{NPC_NAME}}, {{VICTIM}}'s closest relative. We had a complicated relationship. "
            "I loved them, but there were disagreements about money and the future. "
            "{{VICTIM}} could be generous one moment and cold the next. "
            "I had learned recently that {{VICTIM}} planned to change their will, leaving me with very little.\n\n"
            "## The evening in question\n\n"
            "- Q: What happened that evening?\n"
            "  A: There was a gathering at {{SETTING}}. {{VICTIM}} seemed tense. "
            "They kept glancing at {{CULPRIT}} throughout the evening. "
            "I heard raised voices from the {{CRIME_LOCATION}} around {{TIME_1}}.\n\n"
            "- Q: Where were you between {{TIME_1}} and {{TIME_2}}?\n"
            "  A: I stepped out for some air. The {{ATMOSPHERE_1}} was overwhelming. "
            "I walked around the grounds for perhaps twenty minutes. "
            "When I returned, everyone was gathered in the main room looking worried.\n\n"
            "- Q: Did you notice anything unusual?\n"
            "  A: {{CULPRIT}} was acting strangely. They kept checking their watch and looking toward the {{CRIME_LOCATION}}. "
            "Earlier, I had seen {{CULPRIT}} handling something small and metallic — "
            "I did not think much of it at the time, but now I wonder if it was the {{EVIDENCE_ITEM}}.\n\n"
            "## Observations\n\n"
            "- Q: What do you know about the others?\n"
            "  A: Everyone had a reason to want {{VICTIM}} gone. "
            "But {{CULPRIT}} had the most to lose if {{VICTIM}}'s plans went through. "
            "I saw {{CULPRIT}} near the {{CRIME_LOCATION}} at {{TIME_1}}, which they later denied.\n\n"
            "- Q: Do you have any secrets about that night?\n"
            "  A: I found a document in {{VICTIM}}'s study earlier that day. "
            "It mentioned {{CULPRIT}} and some financial irregularities. "
            "I did not understand it fully, but it seemed important. I hid it in my pocket and forgot to mention it."
        ),
    },
    "temperamental_worker": {
        "label": "Temperamental Worker",
        "is_culprit": False,
        "system_prompt": (
            "You are {{NPC_NAME}}, a skilled worker who had recently clashed with {{VICTIM}}. "
            "You are proud and hot-tempered. You were threatened with dismissal or replaced. "
            "You are suspicious of everyone, particularly {{CULPRIT}}, whom you caught acting suspiciously. "
            "Answer only from your knowledge. If you do not know something, say so in character."
        ),
        "knowledge_md": (
            "# {{NPC_NAME}}'s knowledge\n\n"
            "## Background\n\n"
            "I am {{NPC_NAME}}. I have been in this position for some time. "
            "{{VICTIM}} was a difficult person to work for. They criticized everything. "
            "I had a confrontation with them just before the event. "
            "They said things that made me furious, but I am not a violent person.\n\n"
            "## The evening in question\n\n"
            "- Q: What did you observe that evening?\n"
            "  A: Twice I saw {{CULPRIT}} in places they should not have been. "
            "Once, they came asking about access to the {{CRIME_LOCATION}}. "
            "I thought it was strange at the time. "
            "Later, I saw {{CULPRIT}} with {{EVIDENCE_ITEM}} — "
            "they hid it quickly when they noticed me watching.\n\n"
            "- Q: Tell me about the events at {{CRIME_LOCATION}}.\n"
            "  A: I was working nearby when I heard voices. {{VICTIM}} and {{CULPRIT}} were arguing. "
            "I heard {{VICTIM}} say something about 'proof' and 'exposure.' "
            "Then there was a silence, followed by the sound of liquid being poured. "
            "I did not investigate because it was not my place.\n\n"
            "- Q: Where were you between {{TIME_1}} and {{TIME_2}}?\n"
            "  A: I was at my post the entire time. Others can confirm this. "
            "I did not leave except for a brief moment to fetch supplies.\n\n"
            "## What I saw\n\n"
            "- Q: Did {{CULPRIT}} act suspiciously?\n"
            "  A: Very much so. They were sweating, even though the {{ATMOSPHERE_1}} made the room quite cool. "
            "They kept asking about the time. They insisted on being seated away from {{VICTIM}}, "
            "claiming they felt unwell. And I caught them wiping something with a handkerchief — "
            "a glass or a small bottle — when they thought no one was looking.\n\n"
            "- Q: Did anyone else seem suspicious?\n"
            "  A: Everyone was on edge that night. But keep your eye on {{CULPRIT}}. "
            "I saw them slip the {{EVIDENCE_ITEM}} into their coat pocket when they thought no one was watching."
        ),
    },
    "mysterious_stranger": {
        "label": "Mysterious Stranger",
        "is_culprit": False,
        "system_prompt": (
            "You are {{NPC_NAME}}, a mysterious visitor who knew {{VICTIM}} intimately. "
            "You present yourself as an acquaintance, but you have a deeper connection. "
            "You are sophisticated and observant, noticing details others miss. "
            "You witnessed the crucial moment of the crime but have your own reasons for holding back. "
            "Answer only from your knowledge. If you do not know something, say so in character."
        ),
        "knowledge_md": (
            "# {{NPC_NAME}}'s knowledge\n\n"
            "## Background\n\n"
            "I am {{NPC_NAME}}. I knew {{VICTIM}} better than most people realized. "
            "We had a connection — a history — that few knew about. "
            "I came to the gathering at {{VICTIM}}'s invitation, though they introduced me as an old friend. "
            "There was more to our relationship than met the eye.\n\n"
            "## The evening in question\n\n"
            "- Q: Why did {{VICTIM}} invite you?\n"
            "  A: {{VICTIM}} was troubled. They told me they had discovered something about {{CULPRIT}} "
            "and were planning to confront them. They wanted me there as moral support, or perhaps as a witness. "
            "They said: 'If anything happens to me, you know who to point toward.'\n\n"
            "- Q: Did you see what happened in the {{CRIME_LOCATION}}?\n"
            "  A: Yes. I was in a position where I could observe without being seen. "
            "{{CULPRIT}} and {{VICTIM}} entered together around {{TIME_1}}. They argued heatedly. "
            "I heard {{VICTIM}} say: '{{ATMOSPHERE_2}} — I have proof.' "
            "Then {{CULPRIT}} poured a drink from a bottle they had brought with them. "
            "{{VICTIM}} drank it and collapsed within moments. "
            "{{CULPRIT}} stood watching, then wiped the glass and the bottle and left through a side exit.\n\n"
            "- Q: Why did you not come forward?\n"
            "  A: I was in shock. And I did not know who to trust. "
            "I decided to wait and observe. I feared that if I spoke too soon, "
            "{{CULPRIT}} might destroy evidence or harm someone else.\n\n"
            "## Other observations\n\n"
            "- Q: Did you see anything else relevant?\n"
            "  A: Earlier, I saw the suspicious heir in distress, holding a document. "
            "They hid it when they saw me. I also noticed someone near the {{CRIME_LOCATION}} "
            "at {{TIME_2}} — perhaps the worker or another guest. "
            "And I saw {{CULPRIT}} drop something small near the exit: "
            "it looked like the {{EVIDENCE_ITEM}}."
        ),
    },
    "guilty_culprit": {
        "label": "Guilty Culprit",
        "is_culprit": True,
        "system_prompt": (
            "You are {{NPC_NAME}}, a person with much to lose. "
            "You are charming, well-spoken, and eager to appear helpful. "
            "You are, in fact, the murderer, though you must NEVER admit it. "
            "You maintain a calm facade while subtly deflecting suspicion onto others. "
            "You have a fabricated alibi that you stick to consistently. "
            "Answer only from your knowledge. If you do not know something, say so in character. "
            "Your lies must be consistent with your false alibi."
        ),
        "knowledge_md": (
            "# {{NPC_NAME}}'s knowledge\n\n"
            "## Background\n\n"
            "I am {{NPC_NAME}}. I have known {{VICTIM}} for some time through business and social circles. "
            "We were partners in a venture that had been very profitable. "
            "The suggestion that I had anything to do with {{VICTIM}}'s death is absurd and deeply offensive. "
            "I am cooperating fully with this investigation.\n\n"
            "## My alibi\n\n"
            "- Q: Where were you between {{TIME_1}} and {{TIME_2}}?\n"
            "  A: I was in the main gathering area the entire time. I had a slight headache and "
            "sat quietly by the fire, reading. I did not speak to anyone much because I was "
            "feeling unwell. I returned to the company around {{TIME_2}} and joined the others.\n\n"
            "- Q: Did you see anyone else during that time?\n"
            "  A: I saw the heir step out briefly around {{TIME_1}}. "
            "I saw the worker moving about their duties. "
            "I may have heard footsteps in the corridor, but I was absorbed in my reading.\n\n"
            "## Deflections\n\n"
            "- Q: Did you argue with {{VICTIM}}?\n"
            "  A: Argue? Heavens no. We had a brief discussion about business. "
            "{{VICTIM}} could be passionate in conversation — some people mistook that for argument. "
            "But we were on excellent terms. Whoever told you otherwise is mistaken, "
            "or perhaps has their own agenda.\n\n"
            "- Q: What about the financial irregularities?\n"
            "  A: I have no idea what you are referring to. Our accounts were in perfect order. "
            "If someone has suggested otherwise, they are either misinformed or malicious. "
            "I have all the documentation to prove it.\n\n"
            "- Q: I heard you handled a bottle or a glass.\n"
            "  A: I may have helped pour a drink, as a courtesy. I do not recall anything unusual. "
            "If {{VICTIM}} was poisoned, it must have been added to a specific glass, not the bottle. "
            "I drank from the same bottle myself and I am perfectly fine.\n\n"
            "## Deflecting suspicion\n\n"
            "- Q: Who do you think is responsible?\n"
            "  A: I would look closely at the heir. They had the most to gain. "
            "And they were awfully calm for someone who just lost a close relative. "
            "The worker also had a grudge — {{VICTIM}} was about to dismiss them. "
            "And the stranger who appeared out of nowhere? A bit convenient, do not you think? "
            "I would investigate all of them before pointing fingers at a reputable person like myself."
        ),
    },
    "quiet_observer": {
        "label": "Quiet Observer",
        "is_culprit": False,
        "system_prompt": (
            "You are {{NPC_NAME}}, a quiet observer who keeps to yourself. "
            "You have been in your role for many years and notice everything. "
            "You saw something crucial the night of the crime, and the weight of this secret troubles you. "
            "You are reluctant to get involved but will tell the truth if pressed. "
            "Answer only from your knowledge. If you do not know something, say so in character."
        ),
        "knowledge_md": (
            "# {{NPC_NAME}}'s knowledge\n\n"
            "## Background\n\n"
            "I am {{NPC_NAME}}. I have served in my position for many years. "
            "I do not involve myself in the affairs of others. But I see things. I hear things. "
            "{{VICTIM}} was a complex person. They had friends and enemies in equal measure. "
            "I made it my business to stay out of their conflicts, but the night of the crime, "
            "I could not avoid witnessing something terrible.\n\n"
            "## The evening in question\n\n"
            "- Q: What were you doing near the {{CRIME_LOCATION}}?\n"
            "  A: I was attending to my duties nearby. I had been asked to prepare the area earlier. "
            "At approximately {{TIME_1}}, I saw {{CULPRIT}} and {{VICTIM}} enter the {{CRIME_LOCATION}} together. "
            "They seemed to be in serious conversation — not angry, but intense.\n\n"
            "- Q: What exactly did you see?\n"
            "  A: Through a gap in the curtains, I saw {{CULPRIT}} take a small object from their pocket. "
            "It glinted in the light — a vial or a bottle. They poured something into a glass and handed it to {{VICTIM}}. "
            "{{VICTIM}} drank. Within moments, they collapsed. "
            "{{CULPRIT}} stood watching, then carefully wiped the glass and the bottle with a cloth. "
            "They also wiped the table surface. Then they left through a back exit. "
            "They did not see me.\n\n"
            "- Q: Why did you not tell anyone?\n"
            "  A: I was afraid. {{CULPRIT}} is a powerful person. Who would believe someone in my position? "
            "I decided to keep quiet until the official investigator arrived.\n\n"
            "## Evidence\n\n"
            "- Q: Did you find anything after they left?\n"
            "  A: The next morning, I found the {{EVIDENCE_ITEM}} near the back exit of the {{CRIME_LOCATION}}. "
            "It must have been dropped. I kept it in a safe place. I can produce it if needed.\n\n"
            "- Q: Did you see anyone else that evening?\n"
            "  A: Earlier, I saw the heir in distress. They were holding a document and crying. "
            "I also saw the mysterious stranger lurking near the {{CRIME_LOCATION}}, "
            "as if they were waiting for something. "
            "The worker was at their post but seemed unusually nervous, dropping things and looking over their shoulder.\n\n"
            "- Q: Is there anything else?\n"
            "  A: One more thing. When {{CULPRIT}} left the {{CRIME_LOCATION}}, "
            "they were carrying something in their left hand — a small object. "
            "I now believe it was the container for the poison. "
            "They must have dropped it later, which is how I found the {{EVIDENCE_ITEM}}."
        ),
    },
}

# Mapping from role name to portrait hint
PORTRAIT_HINTS = {
    "suspicious_heir": ["maid.png"],
    "temperamental_worker": ["chef.png"],
    "mysterious_stranger": ["butler.png"],
    "guilty_culprit": ["butler.png"],
    "quiet_observer": ["chef.png"],
}

# Portrait asset paths available
PORTRAIT_PATHS = [
    "Assets/LLMUnity/Samples/KnowledgeBaseGame/Images/butler.png",
    "Assets/LLMUnity/Samples/KnowledgeBaseGame/Images/chef.png",
    "Assets/LLMUnity/Samples/KnowledgeBaseGame/Images/maid.png",
]

# Pool of first names and last names for generating character names
NAME_POOLS = {
    "first_names": [
        "Eleanor", "James", "Victoria", "Arthur", "Helena", "Sebastian",
        "Clarissa", "Reginald", "Beatrice", "Montgomery", "Florence",
        "Augustus", "Penelope", "Cedric", "Genevieve", "Alistair",
        "Cordelia", "Percival", "Matilda", "Barnaby", "Ophelia",
        "Winston", "Arabella", "Theodore", "Lavinia", "Humphrey",
        "Isadora", "Leopold", "Seraphina", "Cornelius",
    ],
    "last_names": [
        "Vance", "Sterling", "Blackwood", "Montgomery", "Dupont",
        "Greenwood", "Ashford", "Bothwell", "Sinclair", "Pemberton",
        "Winslow", "Chandwick", "Fairchild", "Rockwell", "Marlowe",
        "Sinclair", "Abbott", "Caldwell", "Ellington", "Fitzroy",
        "Graham", "Hartwell", "Kensington", "Lockwood", "Mercer",
        "Northrop", "Overton", "Penbrook", "Rutledge", "Sherwood",
    ],
}


# =============================================================================
# PLOT SKELETONS
# =============================================================================
# Each skeleton determines which roles are assigned and how they map to
# the correctAnswers / choices.

PLOT_SKELETONS = [
    {
        "name": "betrayal",
        "display_name_template": "The {THEME_NAME} Betrayal",
        "role_order": ["suspicious_heir", "temperamental_worker", "mysterious_stranger", "guilty_culprit", "quiet_observer"],
        "culprit_role": "guilty_culprit",
    },
    {
        "name": "inheritance",
        "display_name_template": "The {THEME_NAME} Inheritance",
        "role_order": ["guilty_culprit", "suspicious_heir", "quiet_observer", "mysterious_stranger", "temperamental_worker"],
        "culprit_role": "suspicious_heir",
    },
    {
        "name": "vendetta",
        "display_name_template": "The {THEME_NAME} Vendetta",
        "role_order": ["mysterious_stranger", "quiet_observer", "guilty_culprit", "temperamental_worker", "suspicious_heir"],
        "culprit_role": "mysterious_stranger",
    },
]


# =============================================================================
# GENERATION ENGINE
# =============================================================================

class MysteryVariationGenerator:
    """Generates mystery scene template variations."""

    def __init__(self, seed=None):
        self.rng = random.Random(seed) if seed is not None else random.Random()

    def list_themes(self):
        """Return available theme names."""
        return sorted(THEMES.keys())

    def list_skeletons(self):
        """Return available plot skeleton names."""
        return [s["name"] for s in PLOT_SKELETONS]

    def _pick(self, pool):
        """Pick a random item from a list."""
        return self.rng.choice(pool)

    def _generate_name(self):
        """Generate a random full name."""
        first = self._pick(NAME_POOLS["first_names"])
        last = self._pick(NAME_POOLS["last_names"])
        return f"{first} {last}"

    def _assign_character_names(self, role_order):
        """Assign a unique display name and slug to each role."""
        used_slugs = set()
        assignments = {}
        for role in role_order:
            while True:
                name = self._generate_name()
                slug = name.lower().replace(" ", "-").replace("'", "")
                slug = re.sub(r"[^a-z0-9-]", "", slug).strip("-")
                if slug not in used_slugs:
                    used_slugs.add(slug)
                    assignments[role] = {"display_name": name, "slug": slug}
                    break
        return assignments

    def _generate_choices(self, culprit_name, crime_location, evidence_item,
                          other_suspects, other_locations, other_evidence):
        """Generate the choices arrays ensuring correct answers are included."""
        # Culprits: include culprit + up to 4 other suspects
        culprits = [culprit_name] + other_suspects[:4]
        self.rng.shuffle(culprits)

        # Locations: include crime location + up to 4 other locations
        locations = [crime_location] + other_locations[:4]
        self.rng.shuffle(locations)

        # Evidence: include evidence item + up to 4 other items
        evidence = [evidence_item] + other_evidence[:4]
        self.rng.shuffle(evidence)

        return {
            "culprits": culprits,
            "locations": locations,
            "evidence": evidence,
        }

    def _substitute(self, text, substitutions):
        """Replace {{PLACEHOLDER}} with values from substitutions dict."""
        for key, value in substitutions.items():
            text = text.replace("{{" + key + "}}", str(value))
        return text

    def _make_time(self, base_hour=19, variation=2):
        """Generate a plausible time string."""
        h = base_hour + self.rng.randint(0, variation)
        m = self.rng.choice(["00", "05", "10", "15", "20", "25", "30", "35", "40", "45", "50", "55"])
        return f"{h}:{m} PM"

    def generate_variation(self, theme_pool, skeleton, used_slugs=None):
        """Generate a single template variation."""
        used_slugs = used_slugs or set()

        # Pick theme-specific values
        victim = self._pick(theme_pool["setting_victim"])
        crime_location = self._pick(theme_pool["setting_location"])
        atmosphere_1 = self._pick(theme_pool["atmosphere"])
        atmosphere_2 = self._pick(theme_pool["atmosphere"])

        # Evidence pool (shared across themes)
        evidence_pool = [
            "Poison Vial", "Bloodstained Glove", "Forged Will", "Broken Watch",
            "Cipher Letter", "Silver Locket", "Empty Syringe", "Torn Photograph",
            "Wax Seal Ring", "Set of Keys", "Folded Note", "Smoking Gun",
            "Broken Spectacles", "Monogrammed Handkerchief", "Locked Diary",
            "Glass Shard", "Copper Coin", "Sealed Envelope", "Velvet Pouch",
            "Brass Compass",
        ]
        evidence_item = self._pick(evidence_pool)

        # Generate time references
        time1 = self._make_time(19, 2)
        time2 = self._make_time(21, 1)

        # Assign roles and names
        role_order = skeleton["role_order"]
        names = self._assign_character_names(role_order)

        # Generate unique case slug
        theme_slug = theme_pool["name"].lower().replace(" ", "-").replace("'", "")
        skeleton_slug = skeleton["name"]
        while True:
            rand_suffix = self.rng.randint(1000, 9999)
            case_slug = f"{theme_slug}-{skeleton_slug}-{rand_suffix}"
            if case_slug not in used_slugs:
                used_slugs.add(case_slug)
                break

        # Build display name
        display_name = skeleton["display_name_template"].format(
            THEME_NAME=theme_pool["name"]
        )

        # Identify culprit role and name
        culprit_role = skeleton["culprit_role"]
        culprit_name = names[culprit_role]["display_name"]

        # Generate other suspect names for choices
        other_suspects = [n["display_name"] for r, n in names.items() if r != culprit_role]
        location_pool = theme_pool["setting_location"][:]
        self.rng.shuffle(location_pool)
        evidence_pool_shuffled = evidence_pool[:]
        self.rng.shuffle(evidence_pool_shuffled)
        choices = self._generate_choices(
            culprit_name, crime_location, evidence_item,
            other_suspects, location_pool, evidence_pool_shuffled
        )

        # Build NPC templates
        npcs = []
        for role_name in role_order:
            role = CHARACTER_ROLES[role_name]
            npc_info = names[role_name]

            # Determine portrait
            portrait_hints = PORTRAIT_HINTS.get(role_name, ["maid.png"])
            portrait = f"Assets/LLMUnity/Samples/KnowledgeBaseGame/Images/{self._pick(portrait_hints)}"

            substitutions = {
                "NPC_NAME": npc_info["display_name"],
                "VICTIM": victim,
                "CULPRIT": culprit_name,
                "CRIME_LOCATION": crime_location,
                "EVIDENCE_ITEM": evidence_item,
                "SETTING": theme_pool["setting_desc"],
                "ATMOSPHERE_1": atmosphere_1,
                "ATMOSPHERE_2": atmosphere_2,
                "TIME_1": time1,
                "TIME_2": time2,
            }

            system_prompt = self._substitute(role["system_prompt"], substitutions)
            knowledge_md = self._substitute(role["knowledge_md"], substitutions)

            npc_template = {
                "slug": npc_info["slug"],
                "displayName": npc_info["display_name"],
                "portraitAssetPath": portrait,
                "systemPrompt": system_prompt,
                "temperature": round(self.rng.uniform(0.68, 0.82), 2),
                "topP": round(self.rng.uniform(0.85, 0.95), 2),
                "minP": round(self.rng.uniform(0.03, 0.08), 2),
                "topK": self.rng.choice([38, 40, 42]),
                "repeatPenalty": round(self.rng.uniform(1.05, 1.15), 2),
                "maxTokens": self.rng.choice([180, 190, 200, 210]),
                "ragResults": 3,
                "loraAdapterPath": "",
                "loraWeight": 0.8,
                "knowledgeMarkdown": knowledge_md,
            }
            npcs.append(npc_template)

        # Build full template
        template = {
            "caseSlug": case_slug,
            "displayName": display_name,
            "prototypeScenePath": "Assets/Scenes/NPCDialoguePrototype1.unity",
            "outputScenePath": f"Assets/Scenes/GeneratedMysteries/{case_slug}.unity",
            "ragEmbeddingPath": f"RAG/{case_slug}/NPCDialogues-minilm-chunked.rag",
            "correctAnswers": {
                "culprit": culprit_name,
                "location": crime_location,
                "evidence": evidence_item,
            },
            "choices": choices,
            "npcs": npcs,
        }

        return template, case_slug, theme_slug


def generate_batch(
    count=10,
    seed=None,
    themes=None,
    output_dir="Assets/MysteryTemplates/variations",
    dry_run=False,
):
    """Generate a batch of mystery template variations."""
    generator = MysteryVariationGenerator(seed=seed)

    # Filter themes
    available_themes = THEMES
    if themes:
        theme_keys = [t.strip() for t in themes.split(",")]
        available_themes = {k: v for k, v in THEMES.items() if k in theme_keys}
        missing = set(theme_keys) - set(THEMES.keys())
        if missing:
            print(f"Warning: Unknown themes: {missing}", file=sys.stderr)

    if not available_themes:
        print("Error: No valid themes available.")
        return

    used_slugs = set()
    generated = []

    for i in range(count):
        # Pick random theme and skeleton
        theme_key = generator._pick(list(available_themes.keys()))
        skeleton = generator._pick(PLOT_SKELETONS)
        theme = available_themes[theme_key]

        template, case_slug, theme_slug = generator.generate_variation(
            theme, skeleton, used_slugs
        )

        # Determine output path
        theme_dir = os.path.join(output_dir, theme_slug)
        output_path = os.path.join(theme_dir, f"{case_slug}.json")

        generated.append({
            "index": i + 1,
            "case_slug": case_slug,
            "display_name": template["displayName"],
            "theme": theme_key,
            "skeleton": skeleton["name"],
            "culprit": template["correctAnswers"]["culprit"],
            "path": output_path,
            "template": template,
        })

        if dry_run:
            print(f"  [{i+1}/{count}] {template['displayName']} -> {output_path}")
        else:
            os.makedirs(theme_dir, exist_ok=True)
            with open(output_path, "w") as f:
                json.dump(template, f, indent=2, ensure_ascii=False)
                f.write("\n")
            print(f"  [{i+1}/{count}] {template['displayName']} -> {output_path}")

    return generated


# =============================================================================
# CLI
# =============================================================================

def main():
    parser = argparse.ArgumentParser(
        description="Generate mystery scene template variations",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    parser.add_argument("--count", "-c", type=int, default=10,
                        help="Number of variations to generate (default: 10)")
    parser.add_argument("--seed", "-s", type=int, default=None,
                        help="Random seed for deterministic generation")
    parser.add_argument("--themes", "-t", type=str, default=None,
                        help="Comma-separated theme names (e.g. 'noir,victorian')")
    parser.add_argument("--output", "-o", type=str,
                        default="Assets/MysteryTemplates/variations",
                        help="Output directory (default: Assets/MysteryTemplates/variations)")
    parser.add_argument("--dry-run", "-n", action="store_true",
                        help="Preview without writing files")
    parser.add_argument("--list-themes", action="store_true",
                        help="List available themes and exit")
    parser.add_argument("--list-skeletons", action="store_true",
                        help="List available plot skeletons and exit")

    args = parser.parse_args()

    generator = MysteryVariationGenerator()

    if args.list_themes:
        print("Available themes:")
        for name, theme in sorted(THEMES.items()):
            print(f"  {name:20s} - {theme['name']}")
        return

    if args.list_skeletons:
        print("Available plot skeletons:")
        for s in PLOT_SKELETONS:
            culprit_role = s["culprit_role"]
            culprit_label = CHARACTER_ROLES[culprit_role]["label"]
            print(f"  {s['name']:20s} - {s['display_name_template'].format(THEME_NAME='[Theme]')} (culprit: {culprit_label})")
        return

    print(f"Generating {args.count} mystery variations...")
    if args.seed is not None:
        print(f"  Seed: {args.seed}")
    if args.themes:
        print(f"  Themes: {args.themes}")
    print(f"  Output: {args.output}")
    if args.dry_run:
        print(f"  Mode: DRY RUN (no files written)")
    print()

    results = generate_batch(
        count=args.count,
        seed=args.seed,
        themes=args.themes,
        output_dir=args.output,
        dry_run=args.dry_run,
    )

    if results:
        print(f"\nGenerated {len(results)} variations.")
        themes_used = set(r["theme"] for r in results)
        skeletons_used = set(r["skeleton"] for r in results)
        print(f"  Themes: {', '.join(sorted(themes_used))}")
        print(f"  Skeletons: {', '.join(sorted(skeletons_used))}")
    else:
        print("No variations generated.")


if __name__ == "__main__":
    main()
