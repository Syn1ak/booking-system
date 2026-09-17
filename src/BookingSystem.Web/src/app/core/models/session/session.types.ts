import { ICurrentUser } from '../../entities/auth/current-user.dto';

export type TSession = {
  token: string;
  expiresAt: string;
  user: ICurrentUser;
};
