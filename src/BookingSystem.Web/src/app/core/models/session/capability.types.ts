import { Role } from '../../entities/auth/role.enum';

/**
 * The client asks what a user may do, never which role they hold — the same distinction as the
 * server's authorization policies. Changing who may do something is one line here.
 */
export const CAPABILITY_ROLES = {
  canManageRooms: [Role.Admin],
  canViewAllBookings: [Role.Admin],
  canCancelAnyBooking: [Role.Admin],
} as const satisfies Record<string, readonly Role[]>;

export type TCapability = keyof typeof CAPABILITY_ROLES;
