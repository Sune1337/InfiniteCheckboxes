export interface War {
  id: number;
  warLocationId?: string | null;
  createdUtc?: Date;
  startUtc?: Date;
  endUtc?: Date;
  battlefieldWidth: number;
  numberOfChecked: number;
  numberOfUnchecked: number;
  winningTeam?: Team | null;
}

export enum Team {
  checkers,
  uncheckers
}
