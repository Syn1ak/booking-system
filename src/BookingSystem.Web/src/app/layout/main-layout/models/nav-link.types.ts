import { TCapability } from '../../../core/models/session/capability.types';

export type TNavLink = {
  path: string;
  label: string;
  icon: string;
  capability?: TCapability;
};
