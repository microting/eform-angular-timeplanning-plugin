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
    name: 'GLS-A / 3F - Jordbrug Standard',
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
    name: 'GLS-A / 3F - Jordbrug Dyrehold',
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
    name: 'GLS-A / 3F - Jordbrug Elev u18',
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
    name: 'GLS-A / 3F - Jordbrug Elev o18',
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
    name: 'GLS-A / 3F - Jordbrug Elev u18 Dyrehold',
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
];
