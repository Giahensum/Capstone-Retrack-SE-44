export interface AuthResponseDto {
  token: string
  role: string
  userId: string
  fullName: string
  email: string
}

export interface AuthUser {
  userId: string
  email: string
  fullName: string
  role: string
}
