export interface IAdminBookingsCriteria {
  roomId?: string | null;
  from?: string | null;
  to?: string | null;
  includeCancelled?: boolean | null;
}
