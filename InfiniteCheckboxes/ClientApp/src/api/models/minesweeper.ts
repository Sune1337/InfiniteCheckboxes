export interface Minesweeper {
  Id: string;
  Width: number;
  FlagLocationId: string;
  SweepLocationId: string;
  CreatedUtc: Date;
  StartUtc?: Date;
  EndUtc?: Date;
  Mines?: number[];
  MineCounts?: { [id: number]: number };
  Score?: number;
}
