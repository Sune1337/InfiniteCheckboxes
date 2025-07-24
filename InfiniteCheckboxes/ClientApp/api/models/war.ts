export interface War {
  Id: number;
  WarLocationId?: string | null;
  CreatedUtc?: Date;
  StartUtc?: Date;
  EndUtc?: Date;
  BattlefieldWidth: number;
  NumberOfChecked: number;
  NumberOfUnchecked: number;
  WinningTeam?: string | null;
}
