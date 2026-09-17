/** Names on the server's `ScheduleHub` and `IScheduleClient`; a mismatch fails silently. */
export const SCHEDULE_HUB = {
  path: '/hub/schedule',
  methods: { watch: 'Watch', unwatch: 'Unwatch' },
  events: { slotChanged: 'SlotChanged', scheduleReset: 'ScheduleReset' },
} as const;
