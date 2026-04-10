export interface PayRuleSetPreset {
  key: string;
  group: string;
  label: string;
  name: string;
  locked: boolean;
  payDayRules: Array<{
    dayCode: string;
    payTierRules: Array<{ order: number; upToSeconds: number | null; payCode: string }>;
  }>;
  payDayTypeRules: Array<{
    dayType: string;
    defaultPayCode: string;
    priority: number;
    timeBandRules: Array<{ startSecondOfDay: number; endSecondOfDay: number; payCode: string; priority: number }>;
  }>;
}

const WEEKDAY_TIME_BANDS_STANDARD = [
  { startSecondOfDay: 14400, endSecondOfDay: 21600, payCode: 'SHIFTED_MORNING', priority: 1 },
  { startSecondOfDay: 21600, endSecondOfDay: 64800, payCode: 'NORMAL', priority: 1 },
  { startSecondOfDay: 64800, endSecondOfDay: 72000, payCode: 'SHIFTED_EVENING', priority: 1 },
];

const WEEKDAY_TIME_BANDS_DYREHOLD = [
  { startSecondOfDay: 0, endSecondOfDay: 18000, payCode: 'ANIMAL_NIGHT', priority: 1 },
  { startSecondOfDay: 18000, endSecondOfDay: 21600, payCode: 'SHIFTED_MORNING', priority: 1 },
  { startSecondOfDay: 21600, endSecondOfDay: 64800, payCode: 'NORMAL', priority: 1 },
  { startSecondOfDay: 64800, endSecondOfDay: 86400, payCode: 'SHIFTED_EVENING', priority: 1 },
];

// KA Landbrug: normal hours 06:00-19:00 (not 18:00)
const WEEKDAY_TIME_BANDS_KA_LANDBRUG = [
  { startSecondOfDay: 21600, endSecondOfDay: 68400, payCode: 'NORMAL', priority: 1 },
  { startSecondOfDay: 68400, endSecondOfDay: 86400, payCode: 'SHIFTED_NIGHT', priority: 1 },
  { startSecondOfDay: 0, endSecondOfDay: 21600, payCode: 'SHIFTED_NIGHT', priority: 1 },
];

// KA Gron: 06:00-18:00 normal, 18:00-23:00 evening, 23:00-06:00 night
const WEEKDAY_TIME_BANDS_KA_GRON = [
  { startSecondOfDay: 21600, endSecondOfDay: 64800, payCode: 'NORMAL', priority: 1 },
  { startSecondOfDay: 64800, endSecondOfDay: 82800, payCode: 'SHIFTED_EVENING', priority: 1 },
  { startSecondOfDay: 82800, endSecondOfDay: 86400, payCode: 'SHIFTED_NIGHT', priority: 1 },
  { startSecondOfDay: 0, endSecondOfDay: 21600, payCode: 'SHIFTED_NIGHT', priority: 1 },
];

const WEEKDAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'];

function weekdayTypeRules(
  defaultPayCode: string,
  timeBands: PayRuleSetPreset['payDayTypeRules'][0]['timeBandRules'],
): PayRuleSetPreset['payDayTypeRules'] {
  return WEEKDAYS.map(day => ({
    dayType: day,
    defaultPayCode,
    priority: 1,
    timeBandRules: [...timeBands],
  }));
}

export const PAY_RULE_SET_PRESETS: PayRuleSetPreset[] = [
  // Preset 1: Jordbrug - Standard
  {
    key: 'glsa-jordbrug-standard',
    group: 'GLS-A / 3F',
    label: 'Jordbrug - Standard',
    name: 'GLS-A / 3F - Jordbrug Standard 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
          { order: 2, upToSeconds: 33840, payCode: 'OVERTIME_30' },
          { order: 3, upToSeconds: null, payCode: 'OVERTIME_80' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 21600, payCode: 'SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'SAT_AFTERNOON' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [
      ...weekdayTypeRules('NORMAL', WEEKDAY_TIME_BANDS_STANDARD),
      {
        dayType: 'Saturday',
        defaultPayCode: 'SAT_NORMAL',
        priority: 1,
        timeBandRules: [
          { startSecondOfDay: 21600, endSecondOfDay: 43200, payCode: 'SAT_NORMAL', priority: 1 },
          { startSecondOfDay: 43200, endSecondOfDay: 64800, payCode: 'SAT_AFTERNOON', priority: 1 },
        ],
      },
    ],
  },

  // Preset 2: Jordbrug - Dyrehold
  {
    key: 'glsa-jordbrug-dyrehold',
    group: 'GLS-A / 3F',
    label: 'Jordbrug - Dyrehold',
    name: 'GLS-A / 3F - Jordbrug Dyrehold 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
          { order: 2, upToSeconds: 33840, payCode: 'OVERTIME_30' },
          { order: 3, upToSeconds: null, payCode: 'OVERTIME_80' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 21600, payCode: 'SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'SAT_ANIMAL_AFTERNOON' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ANIMAL_SUN_HOLIDAY' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ANIMAL_SUN_HOLIDAY' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [
      ...weekdayTypeRules('NORMAL', WEEKDAY_TIME_BANDS_DYREHOLD),
      {
        dayType: 'Saturday',
        defaultPayCode: 'SAT_NORMAL',
        priority: 1,
        timeBandRules: [
          { startSecondOfDay: 0, endSecondOfDay: 43200, payCode: 'SAT_NORMAL', priority: 1 },
          { startSecondOfDay: 43200, endSecondOfDay: 86400, payCode: 'SAT_ANIMAL_AFTERNOON', priority: 1 },
        ],
      },
      {
        dayType: 'Sunday',
        defaultPayCode: 'ANIMAL_SUN_HOLIDAY',
        priority: 1,
        timeBandRules: [
          { startSecondOfDay: 0, endSecondOfDay: 86400, payCode: 'ANIMAL_SUN_HOLIDAY', priority: 1 },
        ],
      },
      {
        dayType: 'Holiday',
        defaultPayCode: 'ANIMAL_SUN_HOLIDAY',
        priority: 1,
        timeBandRules: [
          { startSecondOfDay: 0, endSecondOfDay: 86400, payCode: 'ANIMAL_SUN_HOLIDAY', priority: 1 },
        ],
      },
    ],
  },

  // Preset 3: Jordbrug - Elev (under 18)
  {
    key: 'glsa-jordbrug-elev-u18',
    group: 'GLS-A / 3F',
    label: 'Jordbrug - Elev (under 18)',
    name: 'GLS-A / 3F - Jordbrug Elev u18 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_OVERTIME_50' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_OVERTIME_50' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_SUN_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SUN_OT_80' },
        ],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_HOL_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_HOL_OT_80' },
        ],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 4: Jordbrug - Elev (over 18)
  {
    key: 'glsa-jordbrug-elev-o18',
    group: 'GLS-A / 3F',
    label: 'Jordbrug - Elev (over 18)',
    name: 'GLS-A / 3F - Jordbrug Elev o18 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: 33840, payCode: 'ELEV_OVERTIME_30' },
          { order: 3, upToSeconds: null, payCode: 'ELEV_OVERTIME_80' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 21600, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_AFTERNOON' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_SUN_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SUN_OT_80' },
        ],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_HOL_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_HOL_OT_80' },
        ],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 5: Jordbrug - Elev u18 Dyrehold
  {
    key: 'glsa-jordbrug-elev-u18-dyrehold',
    group: 'GLS-A / 3F',
    label: 'Jordbrug - Elev u18 Dyrehold',
    name: 'GLS-A / 3F - Jordbrug Elev u18 Dyrehold 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_OVERTIME_50' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_ANIMAL_AFTERNOON' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_SUN_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SUN_OT_80' },
        ],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_HOL_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_HOL_OT_80' },
        ],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 6: Gartneri - Standard
  {
    key: 'glsa-gartneri-standard',
    group: 'GLS-A / 3F',
    label: 'Gartneri - Standard',
    name: 'GLS-A / 3F - Gartneri Standard 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
          { order: 2, upToSeconds: 33840, payCode: 'OVERTIME_50' },
          { order: 3, upToSeconds: null, payCode: 'OVERTIME_100' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 23400, payCode: 'SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'SAT_AFTERNOON' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [
      ...weekdayTypeRules('NORMAL', WEEKDAY_TIME_BANDS_STANDARD),
      {
        dayType: 'Saturday',
        defaultPayCode: 'SAT_NORMAL',
        priority: 1,
        timeBandRules: [
          { startSecondOfDay: 21600, endSecondOfDay: 45000, payCode: 'SAT_NORMAL', priority: 1 },
          { startSecondOfDay: 45000, endSecondOfDay: 64800, payCode: 'SAT_AFTERNOON', priority: 1 },
        ],
      },
    ],
  },

  // Preset 7: Gartneri - Elev (under 18)
  {
    key: 'glsa-gartneri-elev-u18',
    group: 'GLS-A / 3F',
    label: 'Gartneri - Elev (under 18)',
    name: 'GLS-A / 3F - Gartneri Elev u18 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_OVERTIME_50' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_OVERTIME_50' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_SUN_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SUN_OT_100' },
        ],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_HOL_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_HOL_OT_100' },
        ],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 8: Gartneri - Elev (over 18)
  {
    key: 'glsa-gartneri-elev-o18',
    group: 'GLS-A / 3F',
    label: 'Gartneri - Elev (over 18)',
    name: 'GLS-A / 3F - Gartneri Elev o18 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: 33840, payCode: 'ELEV_OVERTIME_50' },
          { order: 3, upToSeconds: null, payCode: 'ELEV_OVERTIME_100' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 23400, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_AFTERNOON' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_SUN_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SUN_OT_100' },
        ],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_HOL_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_HOL_OT_100' },
        ],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 9: Skovbrug - Standard
  {
    key: 'glsa-skovbrug-standard',
    group: 'GLS-A / 3F',
    label: 'Skovbrug - Standard',
    name: 'GLS-A / 3F - Skovbrug Standard 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
          { order: 2, upToSeconds: 33840, payCode: 'OVERTIME_30' },
          { order: 3, upToSeconds: null, payCode: 'OVERTIME_100' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 21600, payCode: 'SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'SAT_AFTERNOON' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [
      ...weekdayTypeRules('NORMAL', WEEKDAY_TIME_BANDS_STANDARD),
      {
        dayType: 'Saturday',
        defaultPayCode: 'SAT_NORMAL',
        priority: 1,
        timeBandRules: [
          { startSecondOfDay: 21600, endSecondOfDay: 43200, payCode: 'SAT_NORMAL', priority: 1 },
          { startSecondOfDay: 43200, endSecondOfDay: 64800, payCode: 'SAT_AFTERNOON', priority: 1 },
        ],
      },
    ],
  },

  // Preset 10: Skovbrug - Elev (under 18)
  {
    key: 'glsa-skovbrug-elev-u18',
    group: 'GLS-A / 3F',
    label: 'Skovbrug - Elev (under 18)',
    name: 'GLS-A / 3F - Skovbrug Elev u18 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_OVERTIME_30' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_OVERTIME_30' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_SUN_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SUN_OT_100' },
        ],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_HOL_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_HOL_OT_100' },
        ],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 11: Skovbrug - Elev (over 18)
  {
    key: 'glsa-skovbrug-elev-o18',
    group: 'GLS-A / 3F',
    label: 'Skovbrug - Elev (over 18)',
    name: 'GLS-A / 3F - Skovbrug Elev o18 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: 33840, payCode: 'ELEV_OVERTIME_30' },
          { order: 3, upToSeconds: null, payCode: 'ELEV_OVERTIME_100' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 21600, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_AFTERNOON' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_SUN_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SUN_OT_100' },
        ],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_HOL_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_HOL_OT_100' },
        ],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 12: KA Landbrug Svine/Kvaeg - Standard
  {
    key: 'ka-landbrug-svine-standard',
    group: 'KA / Krifa',
    label: 'Landbrug Svine/Kvaeg - Standard',
    name: 'KA / Krifa - Landbrug Svine/Kvaeg Standard 2025-2028',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
          { order: 2, upToSeconds: 33840, payCode: 'OVERTIME_50' },
          { order: 3, upToSeconds: null, payCode: 'OVERTIME_100' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SAT_WORK' }],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [
      ...weekdayTypeRules('NORMAL', WEEKDAY_TIME_BANDS_KA_LANDBRUG),
    ],
  },

  // Preset 13: KA Landbrug Svine/Kvaeg - Elev
  {
    key: 'ka-landbrug-svine-elev',
    group: 'KA / Krifa',
    label: 'Landbrug Svine/Kvaeg - Elev',
    name: 'KA / Krifa - Landbrug Svine/Kvaeg Elev 2025-2028',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_OVERTIME_50' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_OVERTIME_50' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_SUN_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SUN_OT_100' },
        ],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_HOL_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_HOL_OT_100' },
        ],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 14: KA Landbrug Plantebrug - Standard
  {
    key: 'ka-landbrug-plante-standard',
    group: 'KA / Krifa',
    label: 'Landbrug Plantebrug - Standard',
    name: 'KA / Krifa - Landbrug Plantebrug Standard 2025-2028',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
          { order: 2, upToSeconds: 37440, payCode: 'OVERTIME_50' },
          { order: 3, upToSeconds: null, payCode: 'OVERTIME_100' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SAT_WORK' }],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [
      ...weekdayTypeRules('NORMAL', WEEKDAY_TIME_BANDS_KA_LANDBRUG),
    ],
  },

  // Preset 15: KA Landbrug Plantebrug - Elev
  {
    key: 'ka-landbrug-plante-elev',
    group: 'KA / Krifa',
    label: 'Landbrug Plantebrug - Elev',
    name: 'KA / Krifa - Landbrug Plantebrug Elev 2025-2028',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_OVERTIME_50' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_OVERTIME_50' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_SUN_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SUN_OT_100' },
        ],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_HOL_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_HOL_OT_100' },
        ],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 16: KA Landbrug Maskinstation - Standard
  {
    key: 'ka-landbrug-maskin-standard',
    group: 'KA / Krifa',
    label: 'Landbrug Maskinstation - Standard',
    name: 'KA / Krifa - Landbrug Maskinstation Standard 2025-2028',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
          { order: 2, upToSeconds: 33840, payCode: 'OVERTIME_30' },
          { order: 3, upToSeconds: null, payCode: 'OVERTIME_80' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SAT_WORK' }],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [
      ...weekdayTypeRules('NORMAL', WEEKDAY_TIME_BANDS_KA_LANDBRUG),
    ],
  },

  // Preset 17: KA Landbrug Maskinstation - Elev
  {
    key: 'ka-landbrug-maskin-elev',
    group: 'KA / Krifa',
    label: 'Landbrug Maskinstation - Elev',
    name: 'KA / Krifa - Landbrug Maskinstation Elev 2025-2028',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_OVERTIME_30' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_OVERTIME_30' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_SUN_OT_30' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SUN_OT_80' },
        ],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_HOL_OT_30' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_HOL_OT_80' },
        ],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 18: KA Gron - Standard
  {
    key: 'ka-gron-standard',
    group: 'KA / Krifa',
    label: 'Gron - Standard',
    name: 'KA / Krifa - Gron Standard 2025-2028',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
          { order: 2, upToSeconds: 37440, payCode: 'OVERTIME_50' },
          { order: 3, upToSeconds: null, payCode: 'OVERTIME_100' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 21600, payCode: 'SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'SAT_AFTERNOON' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [
      ...weekdayTypeRules('NORMAL', WEEKDAY_TIME_BANDS_KA_GRON),
      {
        dayType: 'Saturday',
        defaultPayCode: 'SAT_NORMAL',
        priority: 1,
        timeBandRules: [
          { startSecondOfDay: 21600, endSecondOfDay: 57600, payCode: 'SAT_NORMAL', priority: 1 },
          { startSecondOfDay: 57600, endSecondOfDay: 86400, payCode: 'SAT_AFTERNOON', priority: 1 },
        ],
      },
      {
        dayType: 'Sunday',
        defaultPayCode: 'SUN_HOLIDAY',
        priority: 1,
        timeBandRules: [
          { startSecondOfDay: 0, endSecondOfDay: 86400, payCode: 'SUN_HOLIDAY', priority: 1 },
        ],
      },
      {
        dayType: 'Holiday',
        defaultPayCode: 'SUN_HOLIDAY',
        priority: 1,
        timeBandRules: [
          { startSecondOfDay: 0, endSecondOfDay: 86400, payCode: 'SUN_HOLIDAY', priority: 1 },
        ],
      },
    ],
  },

  // Preset 19: KA Gron - Elev
  {
    key: 'ka-gron-elev',
    group: 'KA / Krifa',
    label: 'Gron - Elev',
    name: 'KA / Krifa - Gron Elev 2025-2028',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_OVERTIME_50' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_OVERTIME_50' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_SUN_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SUN_OT_100' },
        ],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [
          { order: 1, upToSeconds: 7200, payCode: 'ELEV_HOL_OT_50' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_HOL_OT_100' },
        ],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // ──────────────────────────────────────────────────────────────────────────
  // Golf presets (2)
  // ──────────────────────────────────────────────────────────────────────────

  // Preset 20: Golf - Standard
  {
    key: 'glsa-golf-standard',
    group: 'GLS-A / 3F',
    label: 'Golf - Standard',
    name: 'GLS-A / 3F - Golf Standard 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'OVERTIME_100' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 21600, payCode: 'SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'SAT_AFTERNOON' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 21: Golf - Elev
  {
    key: 'glsa-golf-elev',
    group: 'GLS-A / 3F',
    label: 'Golf - Elev',
    name: 'GLS-A / 3F - Golf Elev 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_OVERTIME_100' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_OVERTIME_100' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ELEV_SUN_OT_100' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ELEV_HOL_OT_100' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // ──────────────────────────────────────────────────────────────────────────
  // Agroindustri presets (16 = 8 sub-sectors x Standard + Elev)
  // ──────────────────────────────────────────────────────────────────────────

  // Preset 22: Agroindustri Fjerkrae - Standard
  {
    key: 'glsa-agro-fjerkrae-standard',
    group: 'GLS-A / 3F',
    label: 'Agroindustri Fjerkrae - Standard',
    name: 'GLS-A / 3F - Agroindustri Fjerkrae Standard 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
          { order: 2, upToSeconds: 33840, payCode: 'OVERTIME_30' },
          { order: 3, upToSeconds: 37440, payCode: 'OVERTIME_50' },
          { order: 4, upToSeconds: null, payCode: 'OVERTIME_100' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 21600, payCode: 'SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'SAT_AFTERNOON' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 23: Agroindustri Fjerkrae - Elev
  {
    key: 'glsa-agro-fjerkrae-elev',
    group: 'GLS-A / 3F',
    label: 'Agroindustri Fjerkrae - Elev',
    name: 'GLS-A / 3F - Agroindustri Fjerkrae Elev 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_OVERTIME_30' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_OVERTIME_30' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ELEV_SUN_OT_100' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ELEV_HOL_OT_100' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 24: Agroindustri Grovvare - Standard
  {
    key: 'glsa-agro-grovvare-standard',
    group: 'GLS-A / 3F',
    label: 'Agroindustri Grovvare - Standard',
    name: 'GLS-A / 3F - Agroindustri Grovvare Standard 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
          { order: 2, upToSeconds: 37440, payCode: 'OVERTIME_40' },
          { order: 3, upToSeconds: null, payCode: 'OVERTIME_100' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 21600, payCode: 'SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'SAT_AFTERNOON' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 25: Agroindustri Grovvare - Elev
  {
    key: 'glsa-agro-grovvare-elev',
    group: 'GLS-A / 3F',
    label: 'Agroindustri Grovvare - Elev',
    name: 'GLS-A / 3F - Agroindustri Grovvare Elev 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_OVERTIME_40' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_OVERTIME_40' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ELEV_SUN_OT_100' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ELEV_HOL_OT_100' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 26: Agroindustri Gulerod - Standard
  {
    key: 'glsa-agro-gulerod-standard',
    group: 'GLS-A / 3F',
    label: 'Agroindustri Gulerod - Standard',
    name: 'GLS-A / 3F - Agroindustri Gulerod Standard 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
          { order: 2, upToSeconds: 33840, payCode: 'OVERTIME_30' },
          { order: 3, upToSeconds: null, payCode: 'OVERTIME_80' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 21600, payCode: 'SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'SAT_AFTERNOON' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 27: Agroindustri Gulerod - Elev
  {
    key: 'glsa-agro-gulerod-elev',
    group: 'GLS-A / 3F',
    label: 'Agroindustri Gulerod - Elev',
    name: 'GLS-A / 3F - Agroindustri Gulerod Elev 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_OVERTIME_30' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_OVERTIME_30' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ELEV_SUN_OT_100' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ELEV_HOL_OT_100' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 28: Agroindustri Kartoffelmel - Standard
  {
    key: 'glsa-agro-kartoffelmel-standard',
    group: 'GLS-A / 3F',
    label: 'Agroindustri Kartoffelmel - Standard',
    name: 'GLS-A / 3F - Agroindustri Kartoffelmel Standard 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
          { order: 2, upToSeconds: 33840, payCode: 'OVERTIME_30' },
          { order: 3, upToSeconds: 37440, payCode: 'OVERTIME_50' },
          { order: 4, upToSeconds: null, payCode: 'OVERTIME_100' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 21600, payCode: 'SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'SAT_AFTERNOON' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 29: Agroindustri Kartoffelmel - Elev
  {
    key: 'glsa-agro-kartoffelmel-elev',
    group: 'GLS-A / 3F',
    label: 'Agroindustri Kartoffelmel - Elev',
    name: 'GLS-A / 3F - Agroindustri Kartoffelmel Elev 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_OVERTIME_30' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_OVERTIME_30' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ELEV_SUN_OT_100' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ELEV_HOL_OT_100' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 30: Agroindustri Kartoffelsorter - Standard
  {
    key: 'glsa-agro-kartoffelsorter-standard',
    group: 'GLS-A / 3F',
    label: 'Agroindustri Kartoffelsorter - Standard',
    name: 'GLS-A / 3F - Agroindustri Kartoffelsorter Standard 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
          { order: 2, upToSeconds: 33840, payCode: 'OVERTIME_30' },
          { order: 3, upToSeconds: null, payCode: 'OVERTIME_80' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 21600, payCode: 'SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'SAT_AFTERNOON' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 31: Agroindustri Kartoffelsorter - Elev
  {
    key: 'glsa-agro-kartoffelsorter-elev',
    group: 'GLS-A / 3F',
    label: 'Agroindustri Kartoffelsorter - Elev',
    name: 'GLS-A / 3F - Agroindustri Kartoffelsorter Elev 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_OVERTIME_30' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_OVERTIME_30' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ELEV_SUN_OT_100' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ELEV_HOL_OT_100' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 32: Agroindustri Lucerne - Standard
  {
    key: 'glsa-agro-lucerne-standard',
    group: 'GLS-A / 3F',
    label: 'Agroindustri Lucerne - Standard',
    name: 'GLS-A / 3F - Agroindustri Lucerne Standard 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
          { order: 2, upToSeconds: 33840, payCode: 'OVERTIME_30' },
          { order: 3, upToSeconds: null, payCode: 'OVERTIME_80' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 21600, payCode: 'SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'SAT_AFTERNOON' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 33: Agroindustri Lucerne - Elev
  {
    key: 'glsa-agro-lucerne-elev',
    group: 'GLS-A / 3F',
    label: 'Agroindustri Lucerne - Elev',
    name: 'GLS-A / 3F - Agroindustri Lucerne Elev 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_OVERTIME_30' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_OVERTIME_30' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ELEV_SUN_OT_100' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ELEV_HOL_OT_100' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 34: Agroindustri Minkfoder - Standard
  {
    key: 'glsa-agro-minkfoder-standard',
    group: 'GLS-A / 3F',
    label: 'Agroindustri Minkfoder - Standard',
    name: 'GLS-A / 3F - Agroindustri Minkfoder Standard 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
          { order: 2, upToSeconds: 33840, payCode: 'OVERTIME_30' },
          { order: 3, upToSeconds: null, payCode: 'OVERTIME_80' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 21600, payCode: 'SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'SAT_AFTERNOON' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 35: Agroindustri Minkfoder - Elev
  {
    key: 'glsa-agro-minkfoder-elev',
    group: 'GLS-A / 3F',
    label: 'Agroindustri Minkfoder - Elev',
    name: 'GLS-A / 3F - Agroindustri Minkfoder Elev 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_OVERTIME_30' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_OVERTIME_30' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ELEV_SUN_OT_100' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ELEV_HOL_OT_100' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 36: Agroindustri Ovrige - Standard
  {
    key: 'glsa-agro-ovrige-standard',
    group: 'GLS-A / 3F',
    label: 'Agroindustri Ovrige - Standard',
    name: 'GLS-A / 3F - Agroindustri Ovrige Standard 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
          { order: 2, upToSeconds: 33840, payCode: 'OVERTIME_30' },
          { order: 3, upToSeconds: null, payCode: 'OVERTIME_80' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 21600, payCode: 'SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'SAT_AFTERNOON' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },

  // Preset 37: Agroindustri Ovrige - Elev
  {
    key: 'glsa-agro-ovrige-elev',
    group: 'GLS-A / 3F',
    label: 'Agroindustri Ovrige - Elev',
    name: 'GLS-A / 3F - Agroindustri Ovrige Elev 2024-2026',
    locked: true,
    payDayRules: [
      {
        dayCode: 'WEEKDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_OVERTIME_30' },
        ],
      },
      {
        dayCode: 'SATURDAY',
        payTierRules: [
          { order: 1, upToSeconds: 28800, payCode: 'ELEV_SAT_NORMAL' },
          { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_OVERTIME_30' },
        ],
      },
      {
        dayCode: 'SUNDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ELEV_SUN_OT_100' }],
      },
      {
        dayCode: 'HOLIDAY',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'ELEV_HOL_OT_100' }],
      },
      {
        dayCode: 'GRUNDLOVSDAG',
        payTierRules: [{ order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' }],
      },
    ],
    payDayTypeRules: [],
  },
];
