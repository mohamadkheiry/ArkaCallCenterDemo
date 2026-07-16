export type Role = 'User' | 'SuperAdmin'

export interface SmartPhoneInfo {
  extension: number | null
  status: string
  welcomeMessageText?: string | null
}

export interface Me {
  id: number
  phoneNumber: string
  firstName?: string | null
  lastName?: string | null
  brandName?: string | null
  role: Role
  profileCompleted: boolean
  voiceName?: string | null
  callMinuteLimit?: number | null
  usedMinutes: number
  receptionNumber?: string | null
  hasAvatar: boolean
  avatarVersion?: number
  smartPhone?: SmartPhoneInfo | null
}
