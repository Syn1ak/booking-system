export interface IRegisterRequest {
  email: string;
  password: string;
}

export interface IRegisterResponse {
  userId: string;
  email: string;
}
