import { IMyBooking } from './my-booking.dto';

export interface IAdminBooking extends IMyBooking {
  userId: string;
  userEmail: string | null;
}
