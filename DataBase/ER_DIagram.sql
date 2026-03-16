CREATE TABLE `Users` (
  `UserId` int PRIMARY KEY,
  `Username` varchar(255),
  `Password` varchar(255),
  `Role` varchar(255),
  `ProfileImage` varchar(255),
  `Email` varchar(255),
  `MobileNumber` varchar(255)
);

CREATE TABLE `Posts` (
  `PostId` int PRIMARY KEY,
  `UserId` int,
  `Content` text,
  `PostImage` varchar(255),
  `Status` varchar(255)
);

CREATE TABLE `Messages` (
  `MessageId` int PRIMARY KEY,
  `SenderId` int,
  `ReceiverId` int,
  `MessageContent` text,
  `TimeStamp` datetime
);

CREATE TABLE `Reports` (
  `ReportId` int PRIMARY KEY,
  `ReportedUserId` int,
  `ReportingUserId` int,
  `Reason` text,
  `TimeStamp` datetime
);

CREATE TABLE `Groups` (
  `GroupId` int PRIMARY KEY,
  `GroupName` varchar(255),
  `GroupMembers` text
);

ALTER TABLE `Posts` ADD FOREIGN KEY (`UserId`) REFERENCES `Users` (`UserId`);

ALTER TABLE `Messages` ADD FOREIGN KEY (`SenderId`) REFERENCES `Users` (`UserId`);

ALTER TABLE `Messages` ADD FOREIGN KEY (`ReceiverId`) REFERENCES `Users` (`UserId`);

ALTER TABLE `Reports` ADD FOREIGN KEY (`ReportedUserId`) REFERENCES `Users` (`UserId`);

ALTER TABLE `Reports` ADD FOREIGN KEY (`ReportingUserId`) REFERENCES `Users` (`UserId`);

ALTER TABLE `Users` ADD FOREIGN KEY (`UserId`) REFERENCES `Groups` (`GroupMembers`);
